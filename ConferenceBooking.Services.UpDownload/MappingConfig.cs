using AutoMapper;
using ConferenceBooking.Services.UpDownload.Models;
using ConferenceBooking.Services.UpDownload.Models.Dto;

namespace ConferenceBooking.Services.UpDownload
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<FeedbackDocuments, FeedbackDocumentsDto>().ReverseMap();
                config.CreateMap<ReportDocuments, ReportDocumentsDto>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
