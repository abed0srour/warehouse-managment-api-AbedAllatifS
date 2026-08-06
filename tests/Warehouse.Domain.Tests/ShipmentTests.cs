using Warehouse.Domain;

namespace Warehouse.Domain.Tests;

public class ShipmentTests
{
    private static Supplier MakeSupplier(bool isActive = true) => new()
    {
        Name = "Acme Supplies",
        Country = "USA",
        ContactEmail = "orders@acme.com",
        PhoneNumber = "555-0100",
        IsActive = isActive
    };

    private static Address MakeAddress() => Address.Create("12 Dock Road", "Beirut", "1107", "Lebanon");

    private static Shipment MakeShipment(Supplier? supplier = null) =>
        Shipment.Create("SHP-0001", supplier ?? MakeSupplier(), MakeAddress(), null);

    // ---------- creation ----------

    [Fact]
    public void Create_ShouldInitializeShipmentAsDraft()
    {
        var supplier = MakeSupplier();

        var shipment = Shipment.Create("SHP-0001", supplier, MakeAddress(), null);

        Assert.Equal("SHP-0001", shipment.ReferenceNumber);
        Assert.Equal(supplier.Id, shipment.SupplierId);
        Assert.Equal(supplier.Name, shipment.SupplierName);
        Assert.Equal(ShipmentStatus.Draft, shipment.Status);
        Assert.Empty(shipment.Lines);
        Assert.Empty(shipment.StatusHistory);
        Assert.Null(shipment.DispatchedAt);
        Assert.Null(shipment.SupplierNotifiedAt);
    }

    [Fact]
    public void Create_WithBlankReferenceNumber_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Shipment.Create("  ", MakeSupplier(), MakeAddress(), null));

        Assert.Equal("Shipment reference number is required.", ex.Message);
    }

    [Fact]
    public void Create_ForInactiveSupplier_ShouldThrowInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Shipment.Create("SHP-0001", MakeSupplier(isActive: false), MakeAddress(), null));

        Assert.Equal("Shipments cannot be created for an inactive supplier.", ex.Message);
    }

    [Fact]
    public void Create_WithPastExpectedDeliveryDate_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Shipment.Create("SHP-0001", MakeSupplier(), MakeAddress(), DateTime.UtcNow.AddDays(-2)));
    }

    [Fact]
    public void Create_ShouldStoreExpectedDeliveryDateAsUnspecifiedKind()
    {
        var shipment = Shipment.Create(
            "SHP-0001", MakeSupplier(), MakeAddress(),
            new DateTime(2027, 6, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(DateTimeKind.Unspecified, shipment.ExpectedDeliveryDate!.Value.Kind);
    }

    [Fact]
    public void Address_ShouldCompareByValue()
    {
        Assert.Equal(
            Address.Create("12 Dock Road", "Beirut", "1107", "Lebanon"),
            Address.Create("12 Dock Road", "Beirut", "1107", "Lebanon"));
    }

    [Fact]
    public void Address_WithoutCity_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Address.Create("12 Dock Road", " ", "1107", "Lebanon"));
    }

    // ---------- assigning products ----------

    [Fact]
    public void AssignProduct_ShouldAddLineWithDenormalisedProductDetails()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        shipment.AssignProduct(product, 3);

        var line = Assert.Single(shipment.Lines);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal("Laptop", line.ProductName);
        Assert.Equal("SKU-001", line.Sku);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(3, shipment.TotalUnits);
    }

    [Fact]
    public void AssignProduct_SameProductTwice_ShouldMergeIntoOneLine()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        shipment.AssignProduct(product, 3);
        shipment.AssignProduct(product, 2);

        Assert.Single(shipment.Lines);
        Assert.Equal(5, shipment.TotalUnits);
    }

    [Fact]
    public void AssignProduct_WithZeroQuantity_ShouldThrowArgumentException()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        var ex = Assert.Throws<ArgumentException>(() => shipment.AssignProduct(product, 0));

        Assert.Equal("Assigned quantity must be greater than zero.", ex.Message);
        Assert.Empty(shipment.Lines);
    }

    [Fact]
    public void AssignProduct_ExceedingStock_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 2);

        var ex = Assert.Throws<InvalidOperationException>(() => shipment.AssignProduct(product, 5));

        Assert.Equal("Cannot assign 5 of 'Laptop': only 2 in stock.", ex.Message);
        Assert.Empty(shipment.Lines);
    }

    [Fact]
    public void AssignProduct_WhenProductIsArchived_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        product.Archive();

        var ex = Assert.Throws<InvalidOperationException>(() => shipment.AssignProduct(product, 1));

        Assert.Equal("Archived products cannot be assigned to a shipment.", ex.Message);
    }

    [Fact]
    public void AssignProduct_AfterDispatch_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        shipment.AssignProduct(product, 1);
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);

        var ex = Assert.Throws<InvalidOperationException>(() => shipment.AssignProduct(product, 1));

        Assert.Equal("Products can only be assigned while the shipment is a draft.", ex.Message);
    }

    [Fact]
    public void RemoveProduct_ShouldDropTheLine()
    {
        var shipment = MakeShipment();
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        shipment.AssignProduct(product, 1);

        shipment.RemoveProduct(product.Id);

        Assert.Empty(shipment.Lines);
    }

    [Fact]
    public void RemoveProduct_ThatWasNeverAssigned_ShouldDoNothing()
    {
        var shipment = MakeShipment();

        shipment.RemoveProduct(Guid.NewGuid());

        Assert.Empty(shipment.Lines);
    }

    [Fact]
    public void Lines_ShouldNotBeMutableFromOutsideTheAggregate()
    {
        var shipment = MakeShipment();

        Assert.IsNotType<List<ShipmentLine>>(shipment.Lines);
    }

    // ---------- delivery state ----------

    [Fact]
    public void UpdateDeliveryState_ToDispatched_ShouldStampDispatchedAtAndRecordHistory()
    {
        var shipment = MakeShipment();
        shipment.AssignProduct(Product.Create("Laptop", "SKU-001", 999.99m, 10), 1);

        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched, "TRK-99", "left the dock");

        Assert.Equal(ShipmentStatus.Dispatched, shipment.Status);
        Assert.NotNull(shipment.DispatchedAt);
        Assert.Equal("TRK-99", shipment.TrackingNumber);

        var change = Assert.Single(shipment.StatusHistory);
        Assert.Equal(ShipmentStatus.Draft, change.FromStatus);
        Assert.Equal(ShipmentStatus.Dispatched, change.ToStatus);
        Assert.Equal("left the dock", change.Note);
    }

    [Fact]
    public void UpdateDeliveryState_ToDispatched_WithNoLines_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            shipment.UpdateDeliveryState(ShipmentStatus.Dispatched));

        Assert.Equal("A shipment cannot be dispatched with no products assigned.", ex.Message);
        Assert.Equal(ShipmentStatus.Draft, shipment.Status);
        Assert.Empty(shipment.StatusHistory);
    }

    [Fact]
    public void UpdateDeliveryState_DraftStraightToDelivered_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();
        shipment.AssignProduct(Product.Create("Laptop", "SKU-001", 999.99m, 10), 1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            shipment.UpdateDeliveryState(ShipmentStatus.Delivered));

        Assert.Equal("Cannot move a shipment from 'Draft' to 'Delivered'.", ex.Message);
    }

    [Fact]
    public void UpdateDeliveryState_ToTheSameState_ShouldThrowInvalidOperationException()
    {
        var shipment = MakeShipment();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            shipment.UpdateDeliveryState(ShipmentStatus.Draft));

        Assert.Equal("Shipment is already in state 'Draft'.", ex.Message);
    }

    [Fact]
    public void UpdateDeliveryState_AfterDelivery_ShouldThrowBecauseDeliveredIsTerminal()
    {
        var shipment = MakeShipment();
        shipment.AssignProduct(Product.Create("Laptop", "SKU-001", 999.99m, 10), 1);
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);
        shipment.UpdateDeliveryState(ShipmentStatus.Delivered);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            shipment.UpdateDeliveryState(ShipmentStatus.Cancelled));

        Assert.Equal("Cannot move a shipment from 'Delivered' to 'Cancelled'.", ex.Message);
        Assert.True(shipment.IsClosed);
    }

    [Fact]
    public void UpdateDeliveryState_FullHappyPath_ShouldRecordEveryTransitionInOrder()
    {
        var shipment = MakeShipment();
        shipment.AssignProduct(Product.Create("Laptop", "SKU-001", 999.99m, 10), 1);

        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);
        shipment.UpdateDeliveryState(ShipmentStatus.InTransit);
        shipment.UpdateDeliveryState(ShipmentStatus.Delivered);

        Assert.Equal(3, shipment.StatusHistory.Count);
        Assert.Equal(
            new[] { ShipmentStatus.Dispatched, ShipmentStatus.InTransit, ShipmentStatus.Delivered },
            shipment.StatusHistory.Select(h => h.ToStatus));
        Assert.NotNull(shipment.DeliveredAt);
    }

    [Fact]
    public void UpdateDeliveryState_WithoutTrackingNumber_ShouldKeepThePreviousOne()
    {
        var shipment = MakeShipment();
        shipment.AssignProduct(Product.Create("Laptop", "SKU-001", 999.99m, 10), 1);
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched, "TRK-99");

        shipment.UpdateDeliveryState(ShipmentStatus.InTransit);

        Assert.Equal("TRK-99", shipment.TrackingNumber);
    }

    [Fact]
    public void UpdateDeliveryState_CancelFromDraft_ShouldBeAllowedWithNoLines()
    {
        var shipment = MakeShipment();

        shipment.UpdateDeliveryState(ShipmentStatus.Cancelled, note: "supplier withdrew");

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.True(shipment.IsClosed);
        Assert.Null(shipment.DispatchedAt);
    }

    // ---------- supplier notification ----------

    [Fact]
    public void RecordSupplierNotification_ShouldStampTheTimeAsUnspecifiedKind()
    {
        var shipment = MakeShipment();

        shipment.RecordSupplierNotification(new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 6, 10, 0, 0), shipment.SupplierNotifiedAt);
        Assert.Equal(DateTimeKind.Unspecified, shipment.SupplierNotifiedAt!.Value.Kind);
        Assert.NotNull(shipment.LastUpdatedAt);
    }

    // ---------- rehydration ----------

    [Fact]
    public void Reconstruct_ShouldRestoreStateWithoutReRunningInvariants()
    {
        var supplierId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        var shipment = Shipment.Reconstruct(
            shipmentId,
            "SHP-0001",
            supplierId,
            "Acme Supplies",
            ShipmentStatus.Delivered,
            MakeAddress(),
            expectedDeliveryDate: new DateTime(2020, 1, 1),
            trackingNumber: "TRK-1",
            dispatchedAt: new DateTime(2020, 1, 1),
            deliveredAt: new DateTime(2020, 1, 3),
            supplierNotifiedAt: null,
            createdAt: new DateTime(2019, 12, 30),
            lastUpdatedAt: new DateTime(2020, 1, 3),
            lines: new[] { ShipmentLine.Reconstruct(Guid.NewGuid(), shipmentId, Guid.NewGuid(), "Laptop", "SKU-001", 4) },
            statusHistory: Array.Empty<ShipmentStatusChange>());

        // A past delivery date would have been rejected by Create; rehydration accepts it.
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(new DateTime(2020, 1, 1), shipment.ExpectedDeliveryDate);
        Assert.Equal(4, shipment.TotalUnits);
    }

    [Fact]
    public void Reconstruct_ShouldOrderStatusHistoryChronologically()
    {
        var shipmentId = Guid.NewGuid();
        var later = ShipmentStatusChange.Reconstruct(
            Guid.NewGuid(), shipmentId, ShipmentStatus.Dispatched, ShipmentStatus.Delivered, null, new DateTime(2026, 3, 2));
        var earlier = ShipmentStatusChange.Reconstruct(
            Guid.NewGuid(), shipmentId, ShipmentStatus.Draft, ShipmentStatus.Dispatched, null, new DateTime(2026, 3, 1));

        var shipment = Shipment.Reconstruct(
            shipmentId, "SHP-0001", Guid.NewGuid(), "Acme Supplies", ShipmentStatus.Delivered, MakeAddress(),
            null, null, null, null, null, new DateTime(2026, 3, 1), null,
            Array.Empty<ShipmentLine>(),
            new[] { later, earlier });

        Assert.Equal(
            new[] { ShipmentStatus.Dispatched, ShipmentStatus.Delivered },
            shipment.StatusHistory.Select(h => h.ToStatus));
    }
}
