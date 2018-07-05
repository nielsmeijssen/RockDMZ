namespace RockDMZ.Features.PriceExtension
{
    using AutoMapper;
    using RockDMZ.Domain;

    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<PriceExtensionProject, Index.Result.PriceExtension>();
            //CreateMap<ApiDatatable, Details.Model>();
            CreateMap<Create.Command, PriceExtensionProject>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());
            CreateMap<PriceExtensionProject, Edit.Command>()
                .ForMember(dest => dest.DatatablesDirectory, opt => opt.Ignore())
                .ForMember(dest => dest.ProductFeedLocations, opt => opt.Ignore())
                .ForMember(dest => dest.ProductPerformanceFeedLocations, opt => opt.Ignore())
                .ReverseMap();
            

            CreateMap<PriceExtensionProject, Delete.Command>()
                .ForMember(dest => dest.DatatablesDirectory, opt => opt.Ignore());
        }
    }
}



