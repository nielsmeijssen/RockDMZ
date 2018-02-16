using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RockDMZ.Features.ApiDatatable
{
    public class Edit
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; }
            public SelectList ServiceAccounts { get; set; }
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

            public async Task<Command> Handle(Query message)
            {
                var rtn = await _db.ApiDatatables.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
                rtn.ServiceAccounts = new SelectList(_db.ServiceAccounts, "Id", "FriendlyName", rtn.ServiceAccountId);
                return rtn;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string LocalFilePath { get; set; }
            public string Url { get; set; }
            public int ServiceAccountId { get; set; }
            public string Name { get; set; }
            public DateTime FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public bool IncludeDateOfDownload { get; set; }
            public int ReloadBufferSizeInDays { get; set; }
            public UpdateSchedule UpdateSchedule { get; set; }
            public int LookbackWindowInDays { get; set; }
            public SelectList ServiceAccounts { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
                RuleFor(m => m.Name).NotNull().Length(3, 50);
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
                var apiDatatable = await _db.ApiDatatables.FindAsync(message.Id);

                message.LocalFilePath = apiDatatable.LocalFilePath;
                message.Url = apiDatatable.Url;

                Mapper.Map(message, apiDatatable);
                
            }
        }
    }
}
