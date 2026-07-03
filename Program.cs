using WarehouseManagement.Api.Services;
using Scalar.AspNetCore; // 1. Add this using directive

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // 2. Add this line right here!
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllers();

app.Run();