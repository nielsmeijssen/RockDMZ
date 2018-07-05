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

namespace RockDMZ.Features.ApiDatatable
{
    public class Create
    {
        public class Query : IRequest<Command>
        {
            
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
                var rtn = new Command();
                rtn.ServiceAccounts = new SelectList(_db.ServiceAccounts, "Id", "FriendlyName", rtn.ServiceAccountId);
                return rtn;
            }
        }

        public class Command : IRequest
        {
            public string LocalFilePath { get; set; }
            public string Url { get; set; }
            public int ServiceAccountId { get; set; }
            public SelectList ServiceAccounts { get; set; }
            public string Name { get; set; }
            public DateTime FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public bool IncludeDateOfDownload { get; set; }
            public int ReloadBufferSizeInDays { get; set; }
            public UpdateSchedule UpdateSchedule { get; set; }
            public int LookbackWindowInDays { get; set; }
            public string DatatablesDirectory { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Name).NotNull().Length(3, 50);
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
                var apiDatatable = Mapper.Map<Command, RockDMZ.Domain.ApiDatatable>(message);
                var guid = Guid.NewGuid();
                apiDatatable.Url = "http://www.searchtechnologies.nl/feeds/" + guid + ".csv";
                apiDatatable.LocalFilePath = message.DatatablesDirectory + guid + ".csv";
                _db.ApiDatatables.Add(apiDatatable);
                _db.SaveChanges();
            }
        }
    }
}
