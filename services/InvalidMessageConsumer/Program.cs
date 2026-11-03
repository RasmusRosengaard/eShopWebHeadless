using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// Læser beskeder fra Invalid Message Channel og printer dem i konsollen.
const string QueueName = "monolith.catalog.invalid";

// Samme opsætning som de andre services: appsettings.{miljø}.json med en rabbitMq-sektion.
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile($"appsettings.{environment}.json")
    .Build();

var section = configuration.GetRequiredSection("rabbitMq");
var factory = new ConnectionFactory
{
    Uri = new Uri($"amqp://{section["user"]}:{section["password"]}@{section["host"]}:{section["port"]}")
};
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (_, message) =>
{
    Console.WriteLine("Ugyldig besked: " + Encoding.UTF8.GetString(message.Body.Span));
    return Task.CompletedTask;
};

// autoAck: beskeden fjernes fra køen, så snart den er leveret hertil.
await channel.BasicConsumeAsync(QueueName, autoAck: true, consumer);

Console.WriteLine($"Lytter på {QueueName}. Tryk Ctrl+C for at stoppe.");

// Vent for evigt. Console.ReadLine virker ikke i Docker (intet tastatur), så appen ville lukke med det samme.
await Task.Delay(Timeout.Infinite);
