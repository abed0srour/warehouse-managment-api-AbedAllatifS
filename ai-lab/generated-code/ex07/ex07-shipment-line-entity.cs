namespace Warehouse.Domain;

using System;

public class ShipmentLine
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public string Sku { get; private set; }
    public int Quantity { get; private set; }

    private ShipmentLine()
    {
    }

    internal static ShipmentLine For(Guid shipmentId, Product product, int quantity) =>
        throw new NotImplementedException();

    public static ShipmentLine Reconstruct(
        Guid id,
        Guid shipmentId,
        Guid productId,
        string productName,
        string sku,
        int quantity) => throw new NotImplementedException();

    internal void IncreaseQuantity(int additional) => throw new NotImplementedException();
}
