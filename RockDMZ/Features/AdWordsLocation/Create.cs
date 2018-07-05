using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Summary description for Class1
/// </summary>
public class Create
{
    public class Query : IRequest<Command>
    {

    }

    public class QueryHandler : IAsyncRequestHandler<Query, Command>
    {
        //private readonly ToolsContext _db;

        //public QueryHandler(ToolsContext db)
        //{
        //    _db = db;
        //}

        public async Task<Command> Handle(Query message)
        {
            var rtn = new Command();
            // rtn.ServiceAccounts = new SelectList(_db.ServiceAccounts, "Id", "FriendlyName", rtn.ServiceAccountId);
            return rtn;
        }
    }

    public class Command : IRequest
    {
        public string Name { get; set; }
        public int KmRange { get; set; }
        public bool MultipleResults { get; set; }
        public OutputFormat OutputFormat { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(m => m.Name).NotNull().Length(3, 100);
        }
    }

    public class Handler : IRequestHandler<Command>
    {
        private readonly ToolsContext _db;

        public Handler(ToolsContext db)
        {
            _db = db;
        }

        public void Handle(Command message)
        {
            var alp = new AdWordsLocationProject();
            alp.Name = message.Name;
            alp.KmRange = message.KmRange;
            alp.MultipleResults = message.MultipleResults;
            alp.OutputFormat = message.OutputFormat;
            _db.AdWordsLocationProjects.Add(alp);
            _db.SaveChanges();
        }
    }
}
