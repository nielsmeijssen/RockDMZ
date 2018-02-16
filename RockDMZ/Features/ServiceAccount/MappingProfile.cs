namespace RockDMZ.Features.ServiceAccount
{
    using AutoMapper;
    using RockDMZ.Domain;

    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ServiceAccount, Index.Result.ServiceAccount>();
            CreateMap<ServiceAccount, Details.Model>();
            CreateMap<Create.Command, ServiceAccount>()
                .ForSourceMember(x => x.JsonSecret, y => y.Ignore())
                .ForMember(dest => dest.Id, opt => opt.Ignore());
            CreateMap<ServiceAccount, Edit.Command>().ReverseMap();
            CreateMap<ServiceAccount, Delete.Command>();
        }
    }
}