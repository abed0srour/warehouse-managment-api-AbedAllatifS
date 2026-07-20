using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products.Commands;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Warehouse.Presentation.Filters;
using Warehouse.Presentation.Middleware;
using Warehouse.Domain;
using Warehouse.Infrastructure.Repositories;
using Serilog;

// Alias to the Db First DbContext (the one your repositories actually depend on)
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ActionLoggingFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<AcceptLanguageHeaderFilter>();
});

// MediatR scans both Application and Infrastructure assemblies
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateProductCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(WarehouseDbContext).Assembly);
});

builder.Services.AddAutoMapper(
    cfg => { },
    typeof(Warehouse.Application.MappingProfile).Assembly,
    typeof(Warehouse.Infrastructure.Mapping.EfMappingProfile).Assembly);

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

// Single source of truth for the DbContext: factory + scoped wrapper
builder.Services.AddDbContextFactory<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<WarehouseDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<WarehouseDbContext>>().CreateDbContext());

// Localization
builder.Services.AddLocalization();

var supportedCultures = new[] { "en", "fr" };

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
});

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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseRequestLocalization();
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

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}