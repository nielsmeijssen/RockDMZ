using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;
using MediatR;
using RockDMZ.Infrastructure;

namespace RockDMZ.Features.ServiceAccount
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
            public ServiceName SelectedServiceName { get; set; }
        }

        public class Result
        {
            public ServiceName SelectedServiceName { get; set; }
            public List<ServiceAccount> ServiceAccounts { get; set; }

            public class ServiceAccount
            {
                public int Id { get; set; }
                public ServiceName ServiceName { get; set; }
                public CredentialType CredentialType { get; set; }
                public string ServiceLocation { get; set; }
                public string Email { get; set; }
                // public string KeyLocation { get; set; }
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

                var serviceAccounts = await _db.ServiceAccounts
                    //.Where(c => !departmentID.HasValue || c.DepartmentID == departmentID)
                    .OrderBy(d => d.ServiceName)
                    .ProjectToListAsync<Result.ServiceAccount>();

                return new Result
                {
                    ServiceAccounts = serviceAccounts,
                    SelectedServiceName = message.SelectedServiceName
                };
            }
        }
    }
}