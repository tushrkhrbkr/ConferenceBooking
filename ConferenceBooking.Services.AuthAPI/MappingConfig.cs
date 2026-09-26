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
                config.CreateMap<UserProfileRegistration, UserProfileRegistrationDto>().ReverseMap();
                config.CreateMap<UserLoginSessions, UserLoginSessionsDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
