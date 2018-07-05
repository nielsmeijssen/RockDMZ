using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PriceExtension
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
                rtn.ProductPerformanceFeedLocations = new SelectList(_db.ApiDatatables, "LocalFilePath", "Name", rtn.ProductPerformanceFeedLocation);
                rtn.ProductFeedLocations = new SelectList(_db.ApiDatatables, "LocalFilePath", "Name", rtn.ProductFeedLocation);
                return rtn;
            }
        }

        public class Command : IRequest
        {
            public string Name { get; set; }
            public string ProductPerformanceFeedLocation { get; set; }
            public SelectList ProductPerformanceFeedLocations { get; set; }
            public string ProductFeedLocation { get; set; }
            public SelectList ProductFeedLocations { get; set; }
            public string PriceExtensionFeedDestinationLocation { get; set; }
            public string DatatablesDirectory { get; set; }

            // public int AdWordsCampaignStructureId { get; set; }

            public string ProcessingQuery { get; set; }
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
                var peProject = Mapper.Map<Command, RockDMZ.Domain.PriceExtensionProject>(message);
                var guid = Guid.NewGuid();
                peProject.PriceExtensionFeedDestinationLocation = message.DatatablesDirectory + guid + ".csv";
                _db.PriceExtensionProjects.Add(peProject);
                _db.SaveChanges();
            }
        }
    }
}
