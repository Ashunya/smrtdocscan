using AutoMapper;
using SmartDocScan.Data;
using ODTasker.Models;

namespace SmartDocScan
{
    public class AutoMapperConfig
    {
        public static void Initialize()
        {
            Mapper.Initialize((config) =>
            {
                config.CreateMap<UserViewModel, User>().ReverseMap();
                config.CreateMap<ExperienceViewModel, UserExperienceBinding>().ReverseMap();
                config.CreateMap<EducationViewModel, UserEducationBinding>().ReverseMap();
            });
        }
    }
}