using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RockDMZ.Features.AdWordsCustomTagLine
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

            public async Task<Command> Handle(Query message)
            {
                var rtn = await _db.AdWordsCustomTagLines.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();
                return rtn;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string ClientName { get; set; }
            public DateTime CreationDate { get; set; }
            public string TargetCategoryLevel1 { get; set; }
            public string TargetCategoryLevel2 { get; set; }
            public string TargetCategoryLevel3 { get; set; }
            public string TargetSourceFeedPromoLine { get; set; }
            public string ProductLevelPromoLine30 { get; set; }
            public string ProductLevelPromoLine50 { get; set; }
            public string ProductLevelPromoLine80 { get; set; }
            public string BrandAwarenessLine30 { get; set; }
            public string BrandAwarenessLine50 { get; set; }
            public string BrandAwarenessLine80 { get; set; }
            public string PromoAwarenessLine30 { get; set; }
            public string PromoAwarenessLine50 { get; set; }
            public string PromoAwarenessLine80 { get; set; }
            public string ActivationLine30 { get; set; }
            public string ActivationLine50 { get; set; }
            public string ActivationLine80 { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
                RuleFor(m => m.ProductLevelPromoLine30).MaximumLength(30);
                RuleFor(m => m.ProductLevelPromoLine50).MaximumLength(50);
                RuleFor(m => m.ProductLevelPromoLine80).MaximumLength(80);
                RuleFor(m => m.BrandAwarenessLine30).MaximumLength(30);
                RuleFor(m => m.BrandAwarenessLine50).MaximumLength(50);
                RuleFor(m => m.BrandAwarenessLine80).MaximumLength(80);
                RuleFor(m => m.PromoAwarenessLine30).MaximumLength(30);
                RuleFor(m => m.PromoAwarenessLine50).MaximumLength(50);
                RuleFor(m => m.PromoAwarenessLine80).MaximumLength(80);
                RuleFor(m => m.ActivationLine30).MaximumLength(30);
                RuleFor(m => m.ActivationLine50).MaximumLength(50);
                RuleFor(m => m.ActivationLine80).MaximumLength(80);
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
                var alp = await _db.AdWordsCustomTagLines.FindAsync(message.Id);

                Mapper.Map(message, alp);
            }
        }
    }
}
