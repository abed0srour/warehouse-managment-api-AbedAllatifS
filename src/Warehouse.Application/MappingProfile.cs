// Warehouse.Application/MappingProfile.cs
using AutoMapper;
using Warehouse.Application.Products;
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
        }
    }
}