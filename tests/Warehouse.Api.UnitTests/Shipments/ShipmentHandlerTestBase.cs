using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Warehouse.Application;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Shipments;

/// <summary>
/// Shared fixture for the shipment command handlers. Unlike the query handlers, these go
/// through repository interfaces, so the collaborators are mocked -- but the mapper is the real
/// <see cref="MappingProfile"/> rather than a stub, because every one of these handlers returns
/// a mapped view model and a hand-written stub would hide a broken mapping.
/// </summary>
public abstract class ShipmentHandlerTestBase
{
    protected Mock<IShipmentRepository> ShipmentRepository { get; } = new();
    protected Mock<ISupplierRepository> SupplierRepository { get; } = new();
    protected Mock<IProductRepository> ProductRepository { get; } = new();
    protected IMapper Mapper { get; }

    protected ShipmentHandlerTestBase()
    {
        Mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }

    protected static Supplier MakeSupplier(bool isActive = true, string contactEmail = "orders@acme.com") => new()
    {
        Name = "Acme Corp",
        Country = "USA",
        ContactEmail = contactEmail,
        PhoneNumber = "555-0100",
        IsActive = isActive
    };

    protected static Address MakeAddress() => Address.Create("12 Dock Road", "Beirut", "1107", "Lebanon");

    protected static Shipment MakeShipment(Supplier? supplier = null, string reference = "SHP-0001") =>
        Shipment.Create(reference, supplier ?? MakeSupplier(), MakeAddress(), null);

    /// <summary>A draft shipment carrying one line, i.e. one that is legal to dispatch.</summary>
    protected static Shipment MakeShipmentWithLine(Supplier? supplier = null, int quantity = 2)
    {
        var shipment = MakeShipment(supplier);
        shipment.AssignProduct(Product.Create("Wireless Mouse", "SKU-001", 19.99m, 50), quantity);
        return shipment;
    }

    protected void GivenShipment(Shipment shipment) =>
        ShipmentRepository
            .Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    protected void GivenSupplier(Supplier supplier) =>
        SupplierRepository
            .Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

    protected void GivenProducts(params Product[] products) =>
        ProductRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                products.Where(p => ids.Contains(p.Id)).ToList());
}
