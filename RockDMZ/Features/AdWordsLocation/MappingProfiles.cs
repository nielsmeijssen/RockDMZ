using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;

namespace RockDMZ.Features.AdWordsLocation
{


    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<AdWordsLocationProject, Index.Result.AdWordsLocationProject>();
            CreateMap<AdWordsLocationProject, Edit.Command>().ReverseMap();
            CreateMap<AdWordsLocationProject, EditLaunch.Command>().ForMember(dest => dest.JsArray, y => y.Ignore());
        }
    }
}
