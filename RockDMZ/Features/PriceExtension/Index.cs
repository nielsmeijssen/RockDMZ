using AutoMapper;
using MediatR;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PriceExtension
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
        }

        public class Result
        {
            public List<PriceExtension> PriceExtensions { get; set; }

            public class PriceExtension
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string PriceExtensionFeedDestinationLocation { get; set; }
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

                var priceExtensionProject = await _db.PriceExtensionProjects
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.Name)
                    .ProjectToListAsync<Result.PriceExtension>();

                return new Result
                {
                    PriceExtensions = priceExtensionProject
                };
            }
        }
    }
}
