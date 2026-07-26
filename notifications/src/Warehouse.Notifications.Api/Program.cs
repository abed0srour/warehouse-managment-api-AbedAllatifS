using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Warehouse.Notifications.Api;
using Warehouse.Notifications.Api.Data;
using Warehouse.Notifications.Api.Messaging;
using Warehouse.Notifications.Api.Preferences;
using Warehouse.Notifications.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

builder.Services.AddAutoMapper(typeof(NotificationMappingProfile));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<NotificationPreferencesOptions>(builder.Configuration.GetSection(NotificationPreferencesOptions.SectionName));
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