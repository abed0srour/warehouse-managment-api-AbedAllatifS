using AutoMapper;
using Warehouse.Notifications.Application.Notifications;
using Warehouse.Notifications.Domain.Entities;

namespace Warehouse.Notifications.Application.Mapping
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<Notification, NotificationViewModel>();
        }
    }
}
