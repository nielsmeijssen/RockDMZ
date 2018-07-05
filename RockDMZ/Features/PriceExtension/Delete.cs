using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PriceExtension
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
                return _db.PriceExtensionProjects.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string ProductPerformanceFeedLocation { get; set; }
            public string ProductFeedLocation { get; set; }
            public string PriceExtensionFeedDestinationLocation { get; set; }
            public string DatatablesDirectory { get; set; }
            // public int AdWordsCampaignStructureId { get; set; }
            public string ProcessingQuery { get; set; }
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
                var dt = await _db.PriceExtensionProjects.FindAsync(message.Id);

                if (File.Exists(dt.PriceExtensionFeedDestinationLocation)) File.Delete(dt.PriceExtensionFeedDestinationLocation);

                _db.PriceExtensionProjects.Remove(dt);
            }
        }
    }
}
