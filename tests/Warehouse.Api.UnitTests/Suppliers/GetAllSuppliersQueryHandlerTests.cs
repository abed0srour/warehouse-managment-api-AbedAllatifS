using AutoMapper;
using FluentAssertions;
using Moq;
using Warehouse.Application.Suppliers;
using Warehouse.Application.Suppliers.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Suppliers;

public class GetAllSuppliersQueryHandlerTests
{
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly GetAllSuppliersQueryHandler _handler;

    public GetAllSuppliersQueryHandlerTests()
    {
        _mapper.Setup(m => m.Map<IEnumerable<SupplierViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<Supplier>)source).Select(s => new SupplierViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Country = s.Country,
                ContactEmail = s.ContactEmail,
                PhoneNumber = s.PhoneNumber,
                IsActive = s.IsActive
            }).ToList());

        _handler = new GetAllSuppliersQueryHandler(_supplierRepository.Object, _mapper.Object);
    }

    private void GivenSuppliers(params Supplier[] suppliers) =>
        _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(suppliers);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ReturnsAllSuppliers()
    {
        GivenSuppliers(
            new Supplier { Name = "Acme", Country = "US" },
            new Supplier { Name = "Globex", Country = "DE" });

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(s => s.Name).Should().BeEquivalentTo("Acme", "Globex");
    }

    [Fact]
    public async Task Handle_MapsAllSupplierFields()
    {
        var supplier = new Supplier
        {
            Name = "Acme",
            Country = "US",
            ContactEmail = "sales@acme.test",
            PhoneNumber = "+1-555-0100",
            IsActive = true
        };
        GivenSuppliers(supplier);

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        var vm = result.Single();
        vm.Id.Should().Be(supplier.Id);
        vm.Name.Should().Be("Acme");
        vm.Country.Should().Be("US");
        vm.ContactEmail.Should().Be("sales@acme.test");
        vm.PhoneNumber.Should().Be("+1-555-0100");
        vm.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IncludesInactiveSuppliers()
    {
        // This query applies no filtering -- deactivated suppliers are still listed.
        GivenSuppliers(
            new Supplier { Name = "Active", IsActive = true },
            new Supplier { Name = "Inactive", IsActive = false });

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(s => !s.IsActive);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_NoSuppliers_ReturnsEmptyCollection()
    {
        GivenSuppliers();

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        _supplierRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        GivenSuppliers();

        await _handler.Handle(new GetAllSuppliersQuery(), cts.Token);

        _supplierRepository.Verify(r => r.GetAllAsync(cts.Token), Times.Once);
    }

    // ---------- edge cases ----------

    [Fact]
    public async Task Handle_MaxLengthSupplierName_IsPreservedIntact()
    {
        var longName = new string('S', 4000);
        GivenSuppliers(new Supplier { Name = longName, Country = "US" });

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Single().Name.Should().Be(longName).And.HaveLength(4000);
    }

    [Fact]
    public async Task Handle_LargeSupplierSet_ReturnsEveryRow()
    {
        var suppliers = Enumerable.Range(0, 5_000)
            .Select(i => new Supplier { Name = $"Supplier {i}", Country = "US" })
            .ToArray();
        GivenSuppliers(suppliers);

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Should().HaveCount(5_000);
    }

    [Fact]
    public async Task Handle_UnicodeCountryNames_ArePreserved()
    {
        GivenSuppliers(new Supplier { Name = "Zürich Imports", Country = "Côte d'Ivoire" });

        var result = await _handler.Handle(new GetAllSuppliersQuery(), CancellationToken.None);

        result.Single().Country.Should().Be("Côte d'Ivoire");
    }
}
