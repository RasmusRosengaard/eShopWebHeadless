using CatalogService.Api.CatalogItemEndpoints;
using CatalogService.Api.Entities;
using CatalogService.Api.Interfaces;
using CatalogService.Api.Messaging;
using CatalogService.Api.Specifications;
using Moq;
using Xunit;

namespace CatalogService.UnitTests.Messaging;

// Sikrer, at CatalogService publicerer det rigtige event til eventbussen, når cataloget ændres.
// Kæden er: endpoint → CatalogEvents (rigtig) → IEventBus (mock). Repository og IUriComposer mockes,
// så testen kører uden database og RabbitMQ.
public class CatalogEventPublishingTests
{
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IRepository<CatalogItem>> _repositoryMock = new();
    private readonly Mock<IUriComposer> _uriComposerMock = new();
    private readonly CatalogEvents _catalogEvents;

    public CatalogEventPublishingTests()
    {
        _uriComposerMock.Setup(u => u.ComposePicUri(It.IsAny<string>())).Returns("http://test/pic.png");
        _catalogEvents = new CatalogEvents(_eventBusMock.Object, _uriComposerMock.Object);
    }

    [Fact]
    public async Task UpdatingCatalogItem_PublishesCatalogItemUpdated()
    {
        // 1. Arrange: Opsæt Mock
        _eventBusMock.Setup(x => x.Publish(
            "catalog.item.updated",
            It.IsAny<object>()
        )).Returns(Task.CompletedTask).Verifiable();

        var item = new CatalogItem(1, 1, "Beskrivelse", "Gammelt navn", 10m, "pic.png");
        _repositoryMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var endpoint = new UpdateCatalogItemEndpoint(_uriComposerMock.Object, _catalogEvents);
        var request = new UpdateCatalogItemRequest
        {
            Id = 5, Name = "Nyt navn", Description = "Ny beskrivelse", Price = 20m, CatalogBrandId = 1, CatalogTypeId = 1
        };

        // 2. Act: Udfør handling
        await endpoint.HandleAsync(request, _repositoryMock.Object);

        // 3. Assert: Verificer publicering
        _eventBusMock.Verify();
    }

    [Fact]
    public async Task DeletingCatalogItem_PublishesCatalogItemDeleted()
    {
        // 1. Arrange
        _eventBusMock.Setup(x => x.Publish(
            "catalog.item.deleted",
            It.IsAny<object>()
        )).Returns(Task.CompletedTask).Verifiable();

        var item = new CatalogItem(1, 1, "Beskrivelse", "Navn", 10m, "pic.png");
        _repositoryMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var endpoint = new DeleteCatalogItemEndpoint(_catalogEvents);

        // 2. Act
        await endpoint.HandleAsync(new DeleteCatalogItemRequest(5), _repositoryMock.Object);

        // 3. Assert
        _eventBusMock.Verify();
    }

    [Fact]
    public async Task CreatingCatalogItem_PublishesCatalogItemCreated()
    {
        // 1. Arrange
        _eventBusMock.Setup(x => x.Publish(
            "catalog.item.created",
            It.IsAny<object>()
        )).Returns(Task.CompletedTask).Verifiable();

        // Intet item med samme navn, og AddAsync returnerer det item, der blev gemt.
        _repositoryMock.Setup(r => r.CountAsync(It.IsAny<CatalogItemNameSpecification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<CatalogItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogItem added, CancellationToken _) => added);

        var endpoint = new CreateCatalogItemEndpoint(_uriComposerMock.Object, _catalogEvents);
        var request = new CreateCatalogItemRequest
        {
            Name = "Nyt item", Description = "Beskrivelse", Price = 15m, CatalogBrandId = 1, CatalogTypeId = 1, PictureUri = "pic.png"
        };

        // 2. Act
        await endpoint.HandleAsync(request, _repositoryMock.Object);

        // 3. Assert
        _eventBusMock.Verify();
    }

    [Fact]
    public async Task UpdatingUnknownCatalogItem_PublishesNothing()
    {
        // 1. Arrange: repository finder intet item
        _repositoryMock.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CatalogItem?)null);

        var endpoint = new UpdateCatalogItemEndpoint(_uriComposerMock.Object, _catalogEvents);
        var request = new UpdateCatalogItemRequest
        {
            Id = 99, Name = "Navn", Description = "Beskrivelse", Price = 20m, CatalogBrandId = 1, CatalogTypeId = 1
        };

        // 2. Act
        await endpoint.HandleAsync(request, _repositoryMock.Object);

        // 3. Assert: .Verifiable() kan kun bekræfte, at noget skete, så her bruges Verify med Times.Never
        _eventBusMock.Verify(x => x.Publish(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
