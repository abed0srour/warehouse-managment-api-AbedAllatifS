namespace Warehouse.Domain;

using System;

/// <summary>
/// One product assigned to a shipment. Part of the Shipment aggregate: it is only ever
/// created or changed through <see cref="Shipment"/>, which is why there is no public
/// constructor and no public setters.
/// </summary>
public class ShipmentLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShipmentId { get; private set; }
    public Guid ProductId { get; private set; }

    // Denormalised at assignment time, the same way Product carries SupplierName, so a
    // shipment still reads correctly after the product is renamed or archived.
    public string ProductName { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }

    private ShipmentLine()
    {
    }

    internal static ShipmentLine For(Guid shipmentId, Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
            throw new ArgumentException("Assigned quantity must be greater than zero.");

        return new ShipmentLine
        {
            ShipmentId = shipmentId,
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            Quantity = quantity
        };
    }

    // Rehydrates a line from persisted state without re-running assignment invariants.
    public static ShipmentLine Reconstruct(
        Guid id,
        Guid shipmentId,
        Guid productId,
        string productName,
        string sku,
        int quantity) => new()
        {
            Id = id,
            ShipmentId = shipmentId,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            Quantity = quantity
        };

    internal void IncreaseQuantity(int additional)
    {
        if (additional <= 0)
            throw new ArgumentException("Assigned quantity must be greater than zero.");

        Quantity += additional;
    }
}
