using AutoMapper;
using MediatR;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.AdWordsLocation
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
        }

        public class Result
        {
            public List<AdWordsLocationProject> AdWordsLocationProjects { get; set; }

            public class AdWordsLocationProject
            {
                public int Id { get; set; }

                public string Name { get; set; }

                public int KmRange { get; set; }

                public bool MultipleResults { get; set; }

                public OutputFormat OutputFormat { get; set; }
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

                var adwordsLocationProjects = await _db.AdWordsLocationProjects
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.Name)
                    .ProjectToListAsync<Result.AdWordsLocationProject>();

                return new Result
                {
                    AdWordsLocationProjects = adwordsLocationProjects
                };
            }
        }
    }
}
