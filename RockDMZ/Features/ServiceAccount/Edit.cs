using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;

namespace RockDMZ.Features.ServiceAccount
{
    public class Edit
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
            public string FriendlyName { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.ServiceName).NotNull();
                RuleFor(m => m.CredentialType).NotNull();
                RuleFor(m => m.ServiceLocation).MaximumLength(255);
                RuleFor(m => m.Email).NotNull().Length(5, 255);
                RuleFor(m => m.Password).NotNull().When(m => m.CredentialType.Equals(CredentialType.Service));
            }
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
                var serviceAccount = await _db.ServiceAccounts.FindAsync(message.Id);
                message.FriendlyName = message.ServiceName + " | " + message.Email;

                Mapper.Map(message, serviceAccount);
            }
        }
    }
}
