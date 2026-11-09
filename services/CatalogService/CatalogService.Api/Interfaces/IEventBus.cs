namespace CatalogService.Api.Interfaces;

/// <summary>
/// Publicerer integration events til message brokeren. Implementeres af RabbitMq.
/// Interfacet gør, at eventbussen kan mockes i tests, så man kan verificere, at et event publiceres, uden RabbitMQ.
/// </summary>
public interface IEventBus
{
    Task Publish(string routingKey, object message);
}
