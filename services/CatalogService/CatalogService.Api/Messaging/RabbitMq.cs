using System.Text.Json;
using RabbitMQ.Client;

namespace CatalogService.Api.Messaging;
public class RabbitMq
{
    public const string ExchangeName = "catalog";

    private readonly Uri _uri;

    public RabbitMq(IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection("rabbitMq");
        _uri = new Uri($"amqp://{section["user"]}:{section["password"]}@{section["host"]}:{section["port"]}"); // reads from appsettings
    }

    public async Task Publish(string routingKey, object message)
    {
        var factory = new ConnectionFactory { Uri = _uri };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // durable: exchangen gemmes på disk og findes stadig, når RabbitMQ genstarter.
        // Ellers forsvinder den og dens bindinger, og beskeder bliver smidt væk, indtil den oprettes igen.
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);

        // Persistent: beskeden skrives til disk, så den overlever en genstart af RabbitMQ,
        // mens den ligger i en kø og venter på consumeren. Ellers mister monolitten ændringer,
        // og dens kopi af kataloget kommer ud af sync. Virker kun, hvis køen også er durable.
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: bytes);
    }
}
