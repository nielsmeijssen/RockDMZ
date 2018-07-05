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
                rtn.ProductFeedLocations = new SelectList(_db.ApiDatatables, "LocalFilePath", "Name", rtn.ProductFeedLocation);
                return rtn;
            }
        }

        public class Command : IRequest
        {
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
                var peProject = Mapper.Map<Command, RockDMZ.Domain.PromotionExtensionProject>(message);
                var guid = Guid.NewGuid();
                peProject.PromotionExtensionFeedDestinationLocation = message.DatatablesDirectory + guid + ".csv";
                _db.PromotionExtensionProjects.Add(peProject);
                _db.SaveChanges();
            }
        }
    }
}
