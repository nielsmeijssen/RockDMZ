using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PromotionExtension
{
    public class Edit
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; }
            public SelectList ProductFeedLocations { get; set; }
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
                var rtn = await _db.PromotionExtensionProjects.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
                rtn.ProductFeedLocations = new SelectList(_db.ApiDatatables, "LocalFilePath", "Name", rtn.ProductFeedLocation);
                return rtn;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string ProductFeedLocation { get; set; }
            public SelectList ProductFeedLocations { get; set; }
            public string PromotionExtensionFeedDestinationLocation { get; set; }
            public int DefaultPromoDurationInDays { get; set; }
            public bool UsePercentages { get; set; }
            public int MinimumPercentage { get; set; }
            public bool UseAmounts { get; set; }
            public int MinimumAmout { get; set; }
            public int OrMinimumAmountPercentage { get; set; }

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
                var peProject = await _db.PromotionExtensionProjects.FindAsync(message.Id);

                message.PromotionExtensionFeedDestinationLocation = peProject.PromotionExtensionFeedDestinationLocation;

                Mapper.Map(message, peProject);

            }
        }
    }
}
