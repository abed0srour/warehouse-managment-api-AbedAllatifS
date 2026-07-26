using AutoMapper;
using Warehouse.Notifications.Api.Entities;
using Warehouse.Notifications.Api.Notifications;

namespace Warehouse.Notifications.Api
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<Notification, NotificationViewModel>();
        }
    }
}