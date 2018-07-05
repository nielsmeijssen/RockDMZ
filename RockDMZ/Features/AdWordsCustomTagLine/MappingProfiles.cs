using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;

namespace RockDMZ.Features.AdWordsCustomTagLine
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Domain.AdWordsCustomTagLine, Index.Result.AdWordsCustomTagLine>();
            CreateMap<Domain.AdWordsCustomTagLine, Edit.Command>().ReverseMap();
            // CreateMap<AdWordsLocationProject, EditLaunch.Command>().ForMember(dest => dest.JsArray, y => y.Ignore());
        }
    }
}
