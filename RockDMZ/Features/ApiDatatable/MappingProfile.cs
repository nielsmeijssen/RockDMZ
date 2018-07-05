namespace RockDMZ.Features.ApiDatatable
{
    using AutoMapper;
    using RockDMZ.Domain;

    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ApiDatatable, Index.Result.ApiDatatable>();
            //CreateMap<ApiDatatable, Details.Model>();
            CreateMap<Create.Command, ApiDatatable>()
                .ForSourceMember(x => x.ServiceAccounts, y => y.Ignore())
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.LastDownload, opt => opt.Ignore())
                .ForMember(dest => dest.CsvViewIds, opt => opt.Ignore())
                .ForMember(dest => dest.LastDateDownloaded, opt => opt.Ignore())
                .ForMember(dest => dest.ApiQuery, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceAccount, opt => opt.Ignore());
            CreateMap<ApiDatatable, Edit.Command>()
                .ForMember(dest => dest.ServiceAccounts, y => y.Ignore())
                .ForMember(dest => dest.ServiceName, y => y.Ignore())
                .ReverseMap();
            CreateMap<ApiDatatable, EditLaunch.Model>()
                .ForMember(dest => dest.ApiResults, y => y.Ignore())
                .ForMember(dest => dest.ApiError, y => y.Ignore());

            CreateMap<ApiDatatable, Delete.Command>();
        }
    }
}



