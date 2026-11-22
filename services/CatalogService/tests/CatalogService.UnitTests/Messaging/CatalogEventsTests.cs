using System.Text.Json;
using CatalogService.Api.Entities;
using CatalogService.Api.Interfaces;
using CatalogService.Api.Messaging;
using FluentAssertions;
using Moq;
using Xunit;

namespace CatalogService.UnitTests.Messaging;

// Solitary unit tests af Messaging Gatewayen CatalogEvents.
// Kun CatalogEvents er rigtig; IEventBus og IUriComposer er mocks, så der er ingen endpoints,
// repositories, database eller RabbitMQ involveret. Beskeden fanges i en Callback på
// IEventBus.Publish og serialiseres med System.Text.Json, så felterne i de anonyme typer kan tjekkes.
public class CatalogEventsTests
{
    private const string ComposedPictureUri = "http://test/images/pic.png";

    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IUriComposer> _uriComposerMock = new();
    private readonly CatalogEvents _catalogEvents;

    private string? _capturedRoutingKey;   // Her gemmes den routing key, som CatalogEvents sender til Publish
    private JsonElement _capturedMessage;  // Her gemmes beskeden, som CatalogEvents sender til Publish (som JSON)
    public CatalogEventsTests()
    {
        _uriComposerMock                                      
            .Setup(u => u.ComposePicUri(It.IsAny<string>()))   // Når ComposePicUri kaldes med en hvilken som helst string...
            .Returns(ComposedPictureUri);                      // ...returner en fast URL, så vi kan tjekke pictureUri i beskeden

        _eventBusMock                                                       // Falsk IEventBus (står i stedet for RabbitMQ)
            .Setup(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()))  // Når Publish kaldes med en hvilken som helst routing key og besked...
            .Callback<string, object>((routingKey, message) =>              // ...så kør denne kode med de argumenter, CatalogEvents sendte med
            {
                _capturedRoutingKey = routingKey;                               // Gem routing key'en, som produktionskoden valgte
                _capturedMessage = JsonSerializer.SerializeToElement(message);  // Gem beskeden som JSON, så felterne i den anonyme type kan læses
            })
            .Returns(Task.CompletedTask);                                   // ...og lad som om publiceringen lykkedes

        _catalogEvents = new CatalogEvents(_eventBusMock.Object, _uriComposerMock.Object); // Den rigtige klasse under test, med mocks i stedet for rigtige afhængigheder
    }

    [Fact]
    public async Task CatalogItemCreated_publishes_with_created_routing_key_once()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemCreated(item);

        // Assert
        _capturedRoutingKey.Should().Be("catalog.item.created");
        _eventBusMock.Verify(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CatalogItemUpdated_publishes_with_updated_routing_key_once()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemUpdated(item);

        // Assert
        _capturedRoutingKey.Should().Be("catalog.item.updated");
        _eventBusMock.Verify(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CatalogItemDeleted_publishes_with_deleted_routing_key_once()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemDeleted(item);

        // Assert
        _capturedRoutingKey.Should().Be("catalog.item.deleted");
        _eventBusMock.Verify(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CatalogItemCreated_message_contains_version_and_item_data()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemCreated(item);

        // Assert
        _capturedMessage.GetProperty("version").GetInt32().Should().Be(item.Version);

        JsonElement data = _capturedMessage.GetProperty("item");
        data.GetProperty("name").GetString().Should().Be(item.Name);
        data.GetProperty("description").GetString().Should().Be(item.Description);
        data.GetProperty("price").GetDecimal().Should().Be(item.Price);
        data.GetProperty("catalogTypeId").GetInt32().Should().Be(item.CatalogTypeId);
        data.GetProperty("catalogBrandId").GetInt32().Should().Be(item.CatalogBrandId);
        data.GetProperty("pictureUri").GetString().Should().Be(ComposedPictureUri);
    }

    [Fact]
    public async Task CatalogItemUpdated_message_contains_version_and_item_data()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemUpdated(item);

        // Assert
        _capturedMessage.GetProperty("version").GetInt32().Should().Be(item.Version);

        JsonElement data = _capturedMessage.GetProperty("item");
        data.GetProperty("name").GetString().Should().Be(item.Name);
        data.GetProperty("description").GetString().Should().Be(item.Description);
        data.GetProperty("price").GetDecimal().Should().Be(item.Price);
        data.GetProperty("catalogTypeId").GetInt32().Should().Be(item.CatalogTypeId);
        data.GetProperty("catalogBrandId").GetInt32().Should().Be(item.CatalogBrandId);
        data.GetProperty("pictureUri").GetString().Should().Be(ComposedPictureUri);
    }

    [Fact]
    public async Task CatalogItemDeleted_message_contains_id_and_next_version_but_no_item()
    {
        // Arrange
        CatalogItem item = new CatalogItem(2, 3, "Beskrivelse", "Navn", 19.95m, "pic.png");

        // Act
        await _catalogEvents.CatalogItemDeleted(item);

        // Assert
        _capturedMessage.GetProperty("id").GetInt32().Should().Be(item.Id);
        _capturedMessage.GetProperty("version").GetInt32().Should().Be(item.Version + 1);
        _capturedMessage.TryGetProperty("item", out _).Should().BeFalse();
    }
}
