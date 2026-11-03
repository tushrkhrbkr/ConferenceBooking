using AutoMapper;
using ConferenceBooking.Services.BookingAPI.Models;
using ConferenceBooking.Services.BookingAPI.Models.Dto;

namespace ConferenceBooking.Services.BookingAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<ConferenceHall, ConferenceHallDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
