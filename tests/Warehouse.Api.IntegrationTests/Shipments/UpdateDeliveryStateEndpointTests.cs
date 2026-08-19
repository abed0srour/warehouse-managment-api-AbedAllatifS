using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Shipments;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Shipments;

/// <summary>
/// POST /api/shipments/{id}/delivery-state — the shipment state machine over HTTP.
/// Each test builds its own shipment through the API so the scenarios stay independent.
/// </summary>
public class UpdateDeliveryStateEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UpdateDeliveryStateEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewReference() => $"SHP-{Guid.NewGuid():N}".Substring(0, 12);

    private async Task<ShipmentViewModel> CreateDraftAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/shipments", new CreateShipmentRequest
        {
            SupplierId = _factory.SeededSupplierOneId,
            DestinationLine1 = "12 Dock Road",
            DestinationCity = "Beirut",
            DestinationPostalCode = "1107",
            DestinationCountry = "Lebanon",
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(14),
            ReferenceNumber = NewReference()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ShipmentViewModel>())!;
    }

    /// <summary>A draft with one line on it, i.e. one that is legal to dispatch.</summary>
    private async Task<ShipmentViewModel> CreateLoadedDraftAsync(int quantity = 2)
    {
        var shipment = await CreateDraftAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/products",
            new AssignProductsToShipmentRequest
            {
                Products = new()
                {
                    new ShipmentProductAssignmentRequest { ProductId = _factory.SeededProductOneId, Quantity = quantity }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ShipmentViewModel>())!;
    }

    private Task<HttpResponseMessage> MoveAsync(Guid id, string status, string? tracking = null, string? note = null) =>
        _client.PostAsJsonAsync(
            $"/api/shipments/{id}/delivery-state",
            new UpdateDeliveryStateRequest { Status = status, TrackingNumber = tracking, Note = note });

    // ---------- happy path ----------

    [Fact]
    public async Task POST_DispatchALoadedDraft_Returns200()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "Dispatched", "TRK-99", "left the dock");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_Dispatch_ResponseCarriesTheNewStateAndTracking()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "Dispatched", "TRK-99");

        var updated = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        updated!.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
        updated.TrackingNumber.Should().Be("TRK-99");
        updated.DispatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_Dispatch_PersistsTheNewState()
    {
        var shipment = await CreateLoadedDraftAsync();

        await MoveAsync(shipment.Id, "Dispatched", "TRK-99");

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.Id == shipment.Id);

        persisted.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
        persisted.TrackingNumber.Should().Be("TRK-99");
        persisted.DispatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_Dispatch_AppendsAStatusHistoryRow()
    {
        var shipment = await CreateLoadedDraftAsync();

        await MoveAsync(shipment.Id, "Dispatched", note: "left the dock");

        using var db = _factory.CreateDbContext();
        var history = await db.ShipmentStatusChanges.AsNoTracking()
            .Where(h => h.ShipmentId == shipment.Id)
            .ToListAsync();

        history.Should().ContainSingle();
        history[0].FromStatus.Should().Be(nameof(ShipmentStatus.Draft));
        history[0].ToStatus.Should().Be(nameof(ShipmentStatus.Dispatched));
        history[0].Note.Should().Be("left the dock");
    }

    [Fact]
    public async Task POST_Dispatch_NotifiesTheSupplierAsASideEffect()
    {
        // End-to-end proof that the ShipmentDeliveryStateChanged subscriber runs: nothing in the
        // request asks for a notification, but the shipment comes back stamped.
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "Dispatched");

        var updated = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        updated!.SupplierNotifiedAt.Should().NotBeNull();

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.Id == shipment.Id);
        persisted.SupplierNotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_FullLifecycle_RecordsEveryTransition()
    {
        var shipment = await CreateLoadedDraftAsync();

        (await MoveAsync(shipment.Id, "Dispatched")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MoveAsync(shipment.Id, "InTransit")).StatusCode.Should().Be(HttpStatusCode.OK);
        var delivered = await MoveAsync(shipment.Id, "Delivered");

        delivered.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await delivered.Content.ReadFromJsonAsync<ShipmentViewModel>();
        updated!.Status.Should().Be(nameof(ShipmentStatus.Delivered));
        updated.DeliveredAt.Should().NotBeNull();

        using var db = _factory.CreateDbContext();
        var history = await db.ShipmentStatusChanges.AsNoTracking()
            .Where(h => h.ShipmentId == shipment.Id)
            .OrderBy(h => h.OccurredAt)
            .ToListAsync();

        history.Select(h => h.ToStatus).Should().ContainInOrder("Dispatched", "InTransit", "Delivered");
    }

    [Fact]
    public async Task POST_StatusNameIsCaseInsensitive()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "dispatched");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_Status_ReflectsTheTransitionsThroughTheTrackingEndpoint()
    {
        var shipment = await CreateLoadedDraftAsync();
        await MoveAsync(shipment.Id, "Dispatched", "TRK-42", "left the dock");

        var response = await _client.GetAsync($"/api/shipments/{shipment.Id}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<ShipmentStatusViewModel>();
        status!.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
        status.TrackingNumber.Should().Be("TRK-42");
        status.IsClosed.Should().BeFalse();
        status.History.Should().ContainSingle().Which.Note.Should().Be("left the dock");
    }

    [Fact]
    public async Task GET_Status_AfterDelivery_ReportsTheShipmentClosed()
    {
        var shipment = await CreateLoadedDraftAsync();
        await MoveAsync(shipment.Id, "Dispatched");
        await MoveAsync(shipment.Id, "Delivered");

        var response = await _client.GetAsync($"/api/shipments/{shipment.Id}/status");

        var status = await response.Content.ReadFromJsonAsync<ShipmentStatusViewModel>();
        status!.IsClosed.Should().BeTrue();
        status.History.Should().HaveCount(2);
    }

    // ---------- negative cases ----------

    [Fact]
    public async Task POST_UnknownShipment_Returns404()
    {
        var response = await MoveAsync(Guid.NewGuid(), "Dispatched");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_UnrecognisedState_Returns400()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "Teleported");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_DispatchingAnEmptyShipment_Returns409AndLeavesItInDraft()
    {
        var shipment = await CreateDraftAsync();

        var response = await MoveAsync(shipment.Id, "Dispatched");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.Id == shipment.Id);
        persisted.Status.Should().Be(nameof(ShipmentStatus.Draft));
    }

    [Fact]
    public async Task POST_SkippingStraightToDelivered_Returns409()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await MoveAsync(shipment.Id, "Delivered");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_MovingAClosedShipment_Returns409()
    {
        var shipment = await CreateLoadedDraftAsync();
        await MoveAsync(shipment.Id, "Cancelled");

        var response = await MoveAsync(shipment.Id, "Dispatched");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_AssigningProductsAfterDispatch_Returns409()
    {
        var shipment = await CreateLoadedDraftAsync();
        await MoveAsync(shipment.Id, "Dispatched");

        var response = await _client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/products",
            new AssignProductsToShipmentRequest
            {
                Products = new()
                {
                    new ShipmentProductAssignmentRequest { ProductId = _factory.SeededProductTwoId, Quantity = 1 }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_MissingStatus_Returns400()
    {
        var shipment = await CreateLoadedDraftAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/delivery-state",
            new UpdateDeliveryStateRequest { Status = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- assignment side effects, needed by the states above ----------

    [Fact]
    public async Task POST_AssignProducts_PersistsTheLine()
    {
        var shipment = await CreateLoadedDraftAsync(quantity: 3);

        using var db = _factory.CreateDbContext();
        var lines = await db.ShipmentLines.AsNoTracking().Where(l => l.ShipmentId == shipment.Id).ToListAsync();

        lines.Should().ContainSingle();
        lines[0].ProductId.Should().Be(_factory.SeededProductOneId);
        lines[0].Sku.Should().Be(CustomWebApplicationFactory.SeededProductOneSku);
        lines[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task POST_AssignSameProductTwice_TopsUpTheExistingLine()
    {
        var shipment = await CreateDraftAsync();
        var request = new AssignProductsToShipmentRequest
        {
            Products = new()
            {
                new ShipmentProductAssignmentRequest { ProductId = _factory.SeededProductOneId, Quantity = 2 }
            }
        };

        await _client.PostAsJsonAsync($"/api/shipments/{shipment.Id}/products", request);
        await _client.PostAsJsonAsync($"/api/shipments/{shipment.Id}/products", request);

        using var db = _factory.CreateDbContext();
        var lines = await db.ShipmentLines.AsNoTracking().Where(l => l.ShipmentId == shipment.Id).ToListAsync();

        lines.Should().ContainSingle().Which.Quantity.Should().Be(4);
    }

    [Fact]
    public async Task POST_AssignQuantityAboveStock_Returns409()
    {
        var shipment = await CreateDraftAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/products",
            new AssignProductsToShipmentRequest
            {
                Products = new()
                {
                    new ShipmentProductAssignmentRequest { ProductId = _factory.SeededProductOneId, Quantity = 9999 }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_NotifySupplier_StampsTheShipment()
    {
        var shipment = await CreateDraftAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/notify-supplier",
            new NotifySupplierRequest { Message = "please confirm the pickup window" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        updated!.SupplierNotifiedAt.Should().NotBeNull();
    }
}
