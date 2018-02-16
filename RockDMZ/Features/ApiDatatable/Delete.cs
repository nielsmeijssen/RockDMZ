using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using AutoMapper;
using RockDMZ.Infrastructure;
using RockDMZ.Domain;
using System.IO;
using System;

namespace RockDMZ.Features.ApiDatatable
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
                return _db.ApiDatatables.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
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
            public Domain.ServiceAccount ServiceAccount { get; set; }
            public string CsvViewIds { get; set; }
            public string ApiQuery { get; set; }
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
                var dt = await _db.ApiDatatables.FindAsync(message.Id);

                if (File.Exists(dt.LocalFilePath)) File.Delete(dt.LocalFilePath);

                _db.ApiDatatables.Remove(dt);
            }
        }
    }
}
