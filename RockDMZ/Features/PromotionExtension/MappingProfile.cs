namespace RockDMZ.Features.PromotionExtension
{
    using AutoMapper;
    using RockDMZ.Domain;

    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<PromotionExtensionProject, Index.Result.PromotionExtension>();
            //CreateMap<ApiDatatable, Details.Model>();
            CreateMap<Create.Command, PromotionExtensionProject>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());
            CreateMap<PromotionExtensionProject, Edit.Command>()
                .ForMember(dest => dest.ProductFeedLocations, opt => opt.Ignore())
                .ReverseMap();


            CreateMap<PromotionExtensionProject, Delete.Command>();
        }
    }
}



