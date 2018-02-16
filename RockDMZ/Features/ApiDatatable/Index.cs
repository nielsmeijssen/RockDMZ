using AutoMapper;
using MediatR;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.ApiDatatable
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
        }

        public class Result
        {
            public List<ApiDatatable> ApiDatatables { get; set; }

            public class ApiDatatable
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string Url { get; set; }
                public DateTime? LastDownload { get; set; }
                public DateTime? LastDateDownloaded { get; set; }
                public bool IsActive { get; set; }
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

                var apiDatatable = await _db.ApiDatatables
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.Name)
                    .ProjectToListAsync<Result.ApiDatatable>();

                return new Result
                {
                    ApiDatatables = apiDatatable
                };
            }
        }
    }
}
