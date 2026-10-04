using AutoMapper;
using ConferenceBooking.Services.AuthAPI.Models;
using ConferenceBooking.Services.AuthAPI.Models.Dto;

namespace ConferenceBooking.Services.AuthAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<UserProfile, UserProfileDto>().ReverseMap();
                config.CreateMap<UserLoginSessions, UserLoginSessionsDto>().ReverseMap();
                config.CreateMap<Department, DepartmentDto>().ReverseMap();
                config.CreateMap<RegistrationRequest, RegistrationRequestDto>().ReverseMap();
                config.CreateMap<PasswordReset, PasswordResetDto>().ReverseMap();
                config.CreateMap<EmailNotificationOutbox, EmailNotificationOutboxDto>().ReverseMap()
            });
            return mappingConfig;
        }
    }
}
