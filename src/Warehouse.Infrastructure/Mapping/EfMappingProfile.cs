namespace Warehouse.Infrastructure.Mapping;

using AutoMapper;
using Warehouse.Application.Products;
using Warehouse.Application.Suppliers;
using EfProduct = Warehouse.Infrastructure.Data.EfModels.Product;
using EfSupplier = Warehouse.Infrastructure.Data.EfModels.Supplier;

public class EfMappingProfile : Profile
{
    public EfMappingProfile()
    {
        CreateMap<EfProduct, ProductViewModel>();

        CreateMap<EfSupplier, SupplierViewModel>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.SupplierId));
    }
}
