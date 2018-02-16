using FluentValidation;
using MediatR;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;

namespace RockDMZ.Features.ServiceAccount
{
    public class Details
    { 
    public class Query : IRequest<Model>
    {
        public int? Id { get; set; }
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(m => m.Id).NotNull();
        }
    }

    public class Model
    {
            public int Id { get; set; }
            public ServiceName ServiceName { get; set; }
            public CredentialType CredentialType { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
        }

    public class Handler : IAsyncRequestHandler<Query, Model>
    {
        private readonly ToolsContext _db;

        public Handler(ToolsContext db)
        {
            _db = db;
        }

        public Task<Model> Handle(Query message)
        {
            return _db.ServiceAccounts.Where(i => i.Id == message.Id).ProjectToSingleOrDefaultAsync<Model>();
        }
    }
    }
}
