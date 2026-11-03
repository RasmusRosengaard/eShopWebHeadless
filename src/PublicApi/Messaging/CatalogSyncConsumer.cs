using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Microsoft.eShopWeb.PublicApi.Messaging;

/// <summary>
/// Holder monolittens Catalog-tabel i sync med CatalogService (eventually consistent).
/// EIP: subscriber på Publish-Subscribe Channel "catalog" som Durable Subscriber (durable kø),
/// Event-Driven Consumer (RabbitMQ pusher beskeder) og Idempotent Receiver (version-tjek).
/// Replicas af monolitten deler køen og er dermed Competing Consumers.
/// </summary>
public class CatalogSyncConsumer : BackgroundService
{
    private const string ExchangeName = "catalog";
    private const string QueueName = "monolith.catalog";
    private const string BindingKey = "catalog.item.*";
    private const string InvalidQueueName = "monolith.catalog.invalid";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogSyncConsumer> _logger;

    public CatalogSyncConsumer(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<CatalogSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Prøv igen, indtil RabbitMQ svarer. Så crasher monolitten ikke, hvis brokeren er nede,
        // og den indhenter ændringerne fra køen, når den kommer op.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Consume(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Kan ikke forbinde til RabbitMQ, prøver igen om 5 sekunder");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task Consume(CancellationToken stoppingToken)
    {
        var section = _configuration.GetRequiredSection("rabbitMq");
        var factory = new ConnectionFactory
        {
            Uri = new Uri($"amqp://{section["user"]}:{section["password"]}@{section["host"]}:{section["port"]}")
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Samme deklaration som CatalogService, så det er ligegyldigt, hvem der starter først.
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);

        // Durable kø: beskeder venter her, mens monolitten er nede (Durable Subscriber).
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(QueueName, ExchangeName, BindingKey);

        // Invalid Message Channel: beskeder, consumeren ikke kan forstå, parkeres her til manuel inspektion.
        // Ingen binding: vi publicerer via default exchange, som leverer til køen med samme navn som routing key.
        await channel.QueueDeclareAsync(InvalidQueueName, durable: true, exclusive: false, autoDelete: false);

        // Én besked ad gangen pr. consumer, så en replica ikke tager flere, end den kan nå.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, message) =>
        {
            try
            {
                await HandleMessage(message.RoutingKey, message.Body.ToArray());

                // Ack først efter at ændringen er gemt. Crasher vi før, leveres beskeden igen.
                await channel.BasicAckAsync(message.DeliveryTag, multiple: false);
            }
            catch (Exception ex) when (ex is JsonException or InvalidMessageException)
            {
                // Invalid Message Channel: beskeden kan aldrig behandles, så den flyttes i stedet for at prøves igen.
                // Fejlen logges her; i køen ligger kun den oprindelige besked.
                // Publish før ack: crasher vi imellem, ender beskeden to gange i invalid-køen frem for at gå tabt.
                _logger.LogError(ex, "Ugyldig besked {RoutingKey}, flyttes til {Queue}", message.RoutingKey, InvalidQueueName);

                var properties = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

                await channel.BasicPublishAsync(exchange: "", routingKey: InvalidQueueName, mandatory: false,
                    basicProperties: properties, body: message.Body);
                await channel.BasicAckAsync(message.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                // Anden fejl (fx database nede eller concurrency-konflikt mellem replicas): prøv én gang til,
                // ellers drop beskeden, så den ikke blokerer køen for evigt. Erstattes af Dead Letter Channel.
                _logger.LogError(ex, "Kunne ikke behandle {RoutingKey}", message.RoutingKey);
                await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: !message.Redelivered);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer);
        _logger.LogInformation("Lytter på {Queue}", QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessage(string routingKey, byte[] body)
    {
        // Tjek beskeden, før databasen røres, så en ugyldig besked altid ender i invalid-køen,
        // også hvis databasen er nede.
        var message = JsonSerializer.Deserialize<CatalogEventMessage>(body, JsonOptions)
            ?? throw new InvalidMessageException("Tom besked");

        if (routingKey != "catalog.item.created" && routingKey != "catalog.item.updated" && routingKey != "catalog.item.deleted")
        {
            throw new InvalidMessageException($"Ukendt routing key {routingKey}");
        }

        if (routingKey != "catalog.item.deleted" && message.Item == null)
        {
            throw new InvalidMessageException("item mangler");
        }

        var id = message.Item?.Id ?? message.Id;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<CatalogItem>>();
        var existing = await repository.GetByIdAsync(id);

        // Idempotent Receiver: dubletter og forældede events (version <= den gemte) ignoreres.
        if (existing != null && message.Version <= existing.Version)
        {
            _logger.LogInformation("Ignorerer {RoutingKey} for item {Id}: version {Version} er ikke nyere end {Stored}",
                routingKey, id, message.Version, existing.Version);
            return;
        }

        if (routingKey == "catalog.item.created")
        {
            var item = message.Item!;
            await repository.AddAsync(CatalogItem.FromCatalogService(item.Id, item.CatalogTypeId, item.CatalogBrandId,
                item.Description, item.Name, item.Price, item.PictureUri, message.Version));
        }
        else if (routingKey == "catalog.item.updated")
        {
            var item = message.Item!;

            // updated kan nå frem før created (eller created er gået tabt), så opret itemet, hvis det mangler.
            if (existing == null)
            {
                await repository.AddAsync(CatalogItem.FromCatalogService(item.Id, item.CatalogTypeId, item.CatalogBrandId,
                    item.Description, item.Name, item.Price, item.PictureUri, message.Version));
            }
            else
            {
                existing.SyncFromCatalogService(item.CatalogTypeId, item.CatalogBrandId,
                    item.Description, item.Name, item.Price, item.PictureUri, message.Version);
                await repository.UpdateAsync(existing);
            }
        }
        else if (routingKey == "catalog.item.deleted")
        {
            if (existing != null)
            {
                existing.MarkAsDeleted(message.Version);
                await repository.UpdateAsync(existing);
            }
        }

        _logger.LogInformation("Synkroniserede {RoutingKey} for item {Id} (version {Version})", routingKey, id, message.Version);
    }

    // Kastes, når beskeden aldrig kan behandles, uanset hvor mange gange den prøves (Invalid Message Channel).
    private class InvalidMessageException : Exception
    {
        public InvalidMessageException(string message) : base(message) { }
    }

    private class CatalogEventMessage
    {
        public int Version { get; set; }
        public int Id { get; set; }
        public CatalogItemMessage? Item { get; set; }
    }

    private class CatalogItemMessage
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PictureUri { get; set; } = string.Empty;
        public int CatalogTypeId { get; set; }
        public int CatalogBrandId { get; set; }
    }
}
