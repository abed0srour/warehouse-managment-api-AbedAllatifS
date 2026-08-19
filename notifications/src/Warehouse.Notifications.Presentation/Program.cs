using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Notifications.Application.EventProcessing;
using Warehouse.Notifications.Application.Mapping;
using Warehouse.Notifications.Domain.Preferences;
using Warehouse.Notifications.Domain.Repositories;
using Warehouse.Notifications.Infrastructure.Configuration;
using Warehouse.Notifications.Infrastructure.Messaging;
using Warehouse.Notifications.Infrastructure.Persistence;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

builder.Services.AddAutoMapper(cfg => { }, typeof(NotificationMappingProfile).Assembly);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ProcessIntegrationEventCommand).Assembly));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<NotificationPreferencesOptions>(builder.Configuration.GetSection(NotificationPreferencesOptions.SectionName));
builder.Services.AddScoped<INotificationPreferenceProvider, OptionsNotificationPreferenceProvider>();
builder.Services.AddHostedService<NotificationEventConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

static void LoadEnvFile()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".env")))
    {
        directory = directory.Parent;
    }

    if (directory is null)
    {
        return;
    }

    foreach (var line in File.ReadAllLines(Path.Combine(directory.FullName, ".env")))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim();

        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
