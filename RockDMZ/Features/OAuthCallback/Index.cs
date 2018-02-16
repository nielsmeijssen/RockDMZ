using MediatR;
using RockDMZ.Infrastructure;
using System.Threading.Tasks;

namespace RockDMZ.Features.OAuthCallback
{
    public class Index
    {
        public class Query : IRequest<Result>
        {
            public string Error { get; set; }
            public string Code { get; set; }
            public string State { get; set; }
        }

        public class Result
        {
            
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
                return new Result
                {
                    
                };
            }
        }
    }
}
