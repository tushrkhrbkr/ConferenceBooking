using AutoMapper;

namespace ConferenceBooking.Services.ReportAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                //config.CreateMap<ConferenceHall, ConferenceHallDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
