// Warehouse.Application/MappingProfile.cs
using AutoMapper;
using Warehouse.Application.Products;
using Warehouse.Application.Shipments;
using Warehouse.Application.Suppliers;
using Warehouse.Domain;


namespace Warehouse.Application
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Product, ProductViewModel>();

            CreateMap<Supplier, SupplierViewModel>();

            // Status maps enum -> its name, and TotalUnits comes off the aggregate's
            // computed property, so the view model needs no logic of its own.
            CreateMap<Shipment, ShipmentViewModel>();
            CreateMap<ShipmentLine, ShipmentLineViewModel>();
            CreateMap<Address, AddressViewModel>();
        }
    }
}