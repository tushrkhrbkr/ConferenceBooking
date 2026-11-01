using AutoMapper;
using ConferenceBooking.Services.EmailAPI.Models;
using ConferenceBooking.Services.EmailAPI.Models.Dto;

namespace ConferenceBooking.Services.EmailAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<EmailTemplate, EmailTemplateDto>().ReverseMap();
                config.CreateMap<EmailTemplateAudit, EmailTemplateAuditDto>().ReverseMap();
                config.CreateMap<EmailNotificationOutbox, EmailNotificationOutboxDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
