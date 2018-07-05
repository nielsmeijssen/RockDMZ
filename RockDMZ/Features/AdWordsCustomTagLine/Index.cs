using MediatR;
using AutoMapper;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.AdWordsCustomTagLine
{
    public class Index
    {
        public class Query : IRequest<Result>
        {

        }

        public class Result
        {
            public List<AdWordsCustomTagLine> AdWordsCustomTagLines { get; set; }

            public class AdWordsCustomTagLine
            {
                public int Id { get; set; }
                public string ClientName { get; set; }
                public DateTime CreationDate { get; set; }
                public string TargetCategoryLevel1 { get; set; }
                public string TargetCategoryLevel2 { get; set; }
                public string TargetCategoryLevel3 { get; set; }
                public string TargetSourceFeedPromoLine { get; set; }
                public string ProductLevelPromoLine30 { get; set; }
                public string BrandAwarenessLine30 { get; set; }
                public string PromoAwarenessLine30 { get; set; }
                public string ActivationLine30 { get; set; }
            }
        }

        public class Handler : IAsyncRequestHandler<Query, Result>
        {
            private readonly ToolsContext _db;

            public Handler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Result> Handle(Query message)
            {
                // ServiceName? serviceName = message.SelectedServiceName;

                var adwordsCustomTagLines = await _db.AdWordsCustomTagLines
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.CreationDate)
                    .ProjectToListAsync<Result.AdWordsCustomTagLine>();

                return new Result
                {
                    AdWordsCustomTagLines = adwordsCustomTagLines
                };
            }
        }
    }
}
