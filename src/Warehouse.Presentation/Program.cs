using MediatR;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Warehouse.Infrastructure.Data;
using Warehouse.Application.Products.Commands;
using Warehouse.Application.Products.Queries;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Application.Suppliers.Queries;
using Warehouse.Presentation.Middleware;
using Warehouse.Domain;

using Warehouse.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateProductCommand).Assembly));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

var urls = app.Urls.ToArray();
var urlsText = string.Join(", ", urls);
var swaggerUrl = urls.Length > 0
    ? $"{urls[0]}/swagger"
    : "http://localhost:5205/swagger";

app.Logger.LogInformation("Swagger available at: {SwaggerUrl}", swaggerUrl);
app.Logger.LogInformation("Application running on: {Urls}", urlsText);

app.Run();