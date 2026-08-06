using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Shipments;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Shipments;

/// <summary>
/// POST /api/shipments — opening a shipment against a supplier.
/// Asserts the HTTP contract and the persisted side effect by reading the row back out of the
/// application's own database, the same way the product endpoint tests do.
/// </summary>
public class CreateShipmentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateShipmentEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private CreateShipmentRequest ValidRequest(string? reference = null) => new()
    {
        SupplierId = _factory.SeededSupplierOneId,
        DestinationLine1 = "12 Dock Road",
        DestinationCity = "Beirut",
        DestinationPostalCode = "1107",
        DestinationCountry = "Lebanon",
        ExpectedDeliveryDate = DateTime.UtcNow.AddDays(14),
        ReferenceNumber = reference
    };

    // Unique per call so tests in this class cannot collide on the reference uniqueness rule.
    private static string NewReference() => $"SHP-{Guid.NewGuid():N}".Substring(0, 12);

    // ---------- status code and headers ----------

    [Fact]
    public async Task POST_ValidShipment_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(NewReference()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_ValidShipment_LocationHeaderResolvesToTheCreatedShipment()
    {
        var reference = NewReference();
        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        response.Headers.Location.Should().NotBeNull();
        var followUp = await _client.GetAsync(response.Headers.Location);

        followUp.StatusCode.Should().Be(HttpStatusCode.OK);
        var shipment = await followUp.Content.ReadFromJsonAsync<ShipmentViewModel>();
        shipment!.ReferenceNumber.Should().Be(reference);
    }

    [Fact]
    public async Task POST_ValidShipment_EchoesCorrelationIdHeader()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipments")
        {
            Content = JsonContent.Create(ValidRequest(NewReference()))
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be(correlationId);
    }

    // ---------- response body ----------

    [Fact]
    public async Task POST_ValidShipment_ResponseBodyMatchesWhatWasSent()
    {
        var reference = NewReference();

        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        var shipment = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        shipment!.ReferenceNumber.Should().Be(reference);
        shipment.SupplierId.Should().Be(_factory.SeededSupplierOneId);
        shipment.SupplierName.Should().Be("Acme Corp");
        shipment.Destination.Line1.Should().Be("12 Dock Road");
        shipment.Destination.City.Should().Be("Beirut");
        shipment.Destination.Country.Should().Be("Lebanon");
    }

    [Fact]
    public async Task POST_ValidShipment_StartsAsAnEmptyDraft()
    {
        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(NewReference()));

        var shipment = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        shipment!.Status.Should().Be(nameof(ShipmentStatus.Draft));
        shipment.Lines.Should().BeEmpty();
        shipment.TotalUnits.Should().Be(0);
        shipment.DispatchedAt.Should().BeNull();
        shipment.SupplierNotifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task POST_ValidShipment_SerialisesStatusAsAName()
    {
        // Pinning the wire contract: the status is a string, not the enum's ordinal.
        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(NewReference()));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        document.RootElement.GetProperty("status").GetString().Should().Be("Draft");
    }

    [Fact]
    public async Task POST_WithoutAReference_ServerGeneratesOne()
    {
        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference: null));

        var shipment = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();
        shipment!.ReferenceNumber.Should().StartWith("SHP-").And.HaveLength(21);
    }

    // ---------- persisted side effects ----------

    [Fact]
    public async Task POST_ValidShipment_PersistsRowToDatabase()
    {
        var reference = NewReference();

        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(s => s.ReferenceNumber == reference);

        persisted.Should().NotBeNull();
        persisted!.SupplierId.Should().Be(_factory.SeededSupplierOneId);
        persisted.SupplierName.Should().Be("Acme Corp");
        persisted.Status.Should().Be(nameof(ShipmentStatus.Draft));
        persisted.DestinationCity.Should().Be("Beirut");
    }

    [Fact]
    public async Task POST_ValidShipment_PersistsTheDestinationAsFlatColumns()
    {
        var reference = NewReference();

        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.ReferenceNumber == reference);

        persisted.DestinationLine1.Should().Be("12 Dock Road");
        persisted.DestinationPostalCode.Should().Be("1107");
        persisted.DestinationCountry.Should().Be("Lebanon");
    }

    [Fact]
    public async Task POST_ValidShipment_PersistedIdMatchesResponseId()
    {
        var reference = NewReference();

        var response = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));
        var returned = await response.Content.ReadFromJsonAsync<ShipmentViewModel>();

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.ReferenceNumber == reference);

        persisted.Id.Should().Be(returned!.Id);
    }

    [Fact]
    public async Task POST_ValidShipment_StoresExpectedDeliveryDateAsUnspecifiedKind()
    {
        // The codebase normalises every timestamp to DateTimeKind.Unspecified before storing.
        var reference = NewReference();
        var request = ValidRequest(reference);
        request.ExpectedDeliveryDate = new DateTime(2027, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        await _client.PostAsJsonAsync("/api/shipments", request);

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.ReferenceNumber == reference);

        persisted.ExpectedDeliveryDate!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        persisted.ExpectedDeliveryDate.Value.Date.Should().Be(new DateTime(2027, 6, 15));
    }

    [Fact]
    public async Task POST_ValidShipment_CreatesNoStatusHistoryYet()
    {
        var reference = NewReference();

        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        using var db = _factory.CreateDbContext();
        var persisted = await db.Shipments.AsNoTracking().SingleAsync(s => s.ReferenceNumber == reference);
        var history = await db.ShipmentStatusChanges.AsNoTracking().CountAsync(h => h.ShipmentId == persisted.Id);

        history.Should().Be(0);
    }

    // ---------- negative cases ----------

    [Fact]
    public async Task POST_UnknownSupplier_Returns404AndPersistsNothing()
    {
        var reference = NewReference();
        var request = ValidRequest(reference);
        request.SupplierId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/shipments", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var db = _factory.CreateDbContext();
        (await db.Shipments.AsNoTracking().AnyAsync(s => s.ReferenceNumber == reference)).Should().BeFalse();
    }

    [Fact]
    public async Task POST_DuplicateReference_Returns409()
    {
        var reference = NewReference();
        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        var duplicate = await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_DuplicateReference_DoesNotPersistASecondRow()
    {
        var reference = NewReference();
        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        await _client.PostAsJsonAsync("/api/shipments", ValidRequest(reference));

        using var db = _factory.CreateDbContext();
        (await db.Shipments.AsNoTracking().CountAsync(s => s.ReferenceNumber == reference)).Should().Be(1);
    }

    [Fact]
    public async Task POST_MissingDestinationCity_Returns400()
    {
        var request = ValidRequest(NewReference());
        request.DestinationCity = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/shipments", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_PastExpectedDeliveryDate_Returns400AndPersistsNothing()
    {
        var reference = NewReference();
        var request = ValidRequest(reference);
        request.ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-1);

        var response = await _client.PostAsJsonAsync("/api/shipments", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var db = _factory.CreateDbContext();
        (await db.Shipments.AsNoTracking().AnyAsync(s => s.ReferenceNumber == reference)).Should().BeFalse();
    }

    [Fact]
    public async Task POST_MalformedJson_Returns400()
    {
        using var content = new StringContent("{ not json at all", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/shipments", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_UnknownShipment_Returns404()
    {
        var response = await _client.GetAsync($"/api/shipments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
