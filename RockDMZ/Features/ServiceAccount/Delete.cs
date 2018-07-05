using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using AutoMapper;
using RockDMZ.Infrastructure;
using RockDMZ.Domain;
using System.IO;

namespace RockDMZ.Features.ServiceAccount
{
    public class Delete
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class QueryHandler : IAsyncRequestHandler<Query, Command>
        {
            private readonly ToolsContext _db;

            public QueryHandler(ToolsContext db)
            {
                _db = db;
            }

            public Task<Command> Handle(Query message)
            {
                return _db.ServiceAccounts.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public ServiceName ServiceName { get; set; }
            public CredentialType CredentialType { get; set; }
            public string ServiceLocation { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string KeyLocation { get; set; }
        }

        public class CommandHandler : IAsyncRequestHandler<Command>
        {
            private readonly ToolsContext _db;

            public CommandHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task Handle(Command message)
            {
                var sa = await _db.ServiceAccounts.FindAsync(message.Id);

                var dt = _db.ApiDatatables.Where(x => x.ServiceAccountId == message.Id);

                foreach(var t in dt)
                {
                    if (File.Exists(t.LocalFilePath)) File.Delete(t.LocalFilePath);
                }

                _db.ServiceAccounts.Remove(sa);
            }
        }
    }
}
