using MediatR;
using Microsoft.EntityFrameworkCore; // Added for EF Core
using Scalar.AspNetCore;
using Warehouse.Application.Products.Commands;
using Warehouse.Application.Products.Queries;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Application.Suppliers.Queries;
using Warehouse.Domain;
using Warehouse.Infrastructure.Data.EfModels; // Added to access your scaffolded DbContext
using Warehouse.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Register your Docker PostgreSQL Database Context
builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Updated MediatR to scan BOTH Application and Infrastructure assemblies
builder.Services.AddMediatR(cfg => 
{
    // Scans your Application layer (Commands / Queries)
    cfg.RegisterServicesFromAssembly(typeof(CreateProductCommand).Assembly);
    
    // Scans your Infrastructure layer (For your new DB-first handlers)
    cfg.RegisterServicesFromAssembly(typeof(WarehouseDbContext).Assembly);
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Warehouse API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllers();

var urls = app.Urls.ToArray();
var urlsText = string.Join(", ", urls);
var swaggerUrl = urls.Length > 0
    ? $"{urls[0]}/swagger"
    : "http://localhost:5205/swagger";

app.Logger.LogInformation("Swagger available at: {SwaggerUrl}", swaggerUrl);
app.Logger.LogInformation("Application running on: {Urls}", urlsText);

app.Run();