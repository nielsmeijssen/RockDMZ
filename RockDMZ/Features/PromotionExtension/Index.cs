using AutoMapper;
using MediatR;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PromotionExtension
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
        }

        public class Result
        {
            public List<PromotionExtension> PromotionExtensions { get; set; }

            public class PromotionExtension
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string PromotionExtensionFeedDestinationLocation { get; set; }
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

                var priceExtensionProject = await _db.PromotionExtensionProjects
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.Name)
                    .ProjectToListAsync<Result.PromotionExtension>();

                return new Result
                {
                    PromotionExtensions = priceExtensionProject
                };
            }
        }
    }
}
