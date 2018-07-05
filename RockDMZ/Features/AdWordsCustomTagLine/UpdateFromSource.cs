using MediatR;
using AutoMapper;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.AdWordsCustomTagLine
{
    public class UpdateFromSource
    {
        public class Query : IRequest<Result>
        {
            public string ClientName { get; set; }
        }

        public class Result
        {
            public int ItemsAdded { get; set; }

            public int ItemsRemoved { get; set; }
        }

        public class Handler : IAsyncRequestHandler<Query, Result>
        {
            private readonly ToolsContext _db;

            public Handler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Result> Handle(Query message)
            {
                var clientName = "BCC";
                var result = new Result();
                var itemsAdded = 0;
                var itemsRemoved = 0;
                // *****    sync sourcefeedpromolines
                // get distinct SourceFeedPromoLine from BCCSourceFeedItems
                var sfpl = _db.BccSourceFeedItems.Select(x => x.SourceFeedPromoLine).Distinct();
                // get all TargetSourceFeedPromoLine from AdWordsCustomTagLines
                var tpl = _db.AdWordsCustomTagLines.Select(x => x.TargetSourceFeedPromoLine).Distinct();
                // remove all TargetSourceFeedPromoLine that are not in SourceFeedPromoLine
                foreach(var t in tpl)
                {
                    if (!String.IsNullOrWhiteSpace(t) && !sfpl.Contains(t))
                    {
                        var del = _db.AdWordsCustomTagLines.Where(x => x.TargetSourceFeedPromoLine == t);
                        _db.AdWordsCustomTagLines.RemoveRange(del);

                        itemsRemoved++;
                    }
                }

                // add all SourceFeedPromoLine that are not in TargetSourceFeedPromoLine
                foreach(var sf in sfpl)
                {
                    if (!tpl.Contains(sf))
                    {
                        _db.AdWordsCustomTagLines.Add(new Domain.AdWordsCustomTagLine() { ClientName = clientName, TargetSourceFeedPromoLine = sf, CreationDate = DateTime.Now
                        });
                        itemsAdded++;
                    }
                }

                // ******   sync cateforylevels
                // get distinct category levels from BCCSourFeedItems
                var dcsf = _db.BccSourceFeedItems.Select(u => new
                {
                    u.CategoryLevel1,
                    u.CategoryLevel2,
                    u.CategoryLevel3
                }).Distinct();
                // get all category levels from ACTL
                var dctl = _db.AdWordsCustomTagLines.Select(v => new
                {
                    v.TargetCategoryLevel1,
                    v.TargetCategoryLevel2,
                    v.TargetCategoryLevel3
                });
                // remove all category levels that are not in sourcefeed
                foreach(var item in dctl)
                {
                    if (String.IsNullOrWhiteSpace(item.TargetCategoryLevel1) && String.IsNullOrWhiteSpace(item.TargetCategoryLevel2) && String.IsNullOrWhiteSpace(item.TargetCategoryLevel3)) continue;

                    if (dcsf.SingleOrDefault(x => x.CategoryLevel1 == item.TargetCategoryLevel1 
                                && x.CategoryLevel2 == item.TargetCategoryLevel2 
                                && x.CategoryLevel3 == item.TargetCategoryLevel3) == null)
                    {
                        var del = _db.AdWordsCustomTagLines.Where(x => x.TargetCategoryLevel1 == item.TargetCategoryLevel1
                            && x.TargetCategoryLevel2 == item.TargetCategoryLevel2
                            && x.TargetCategoryLevel3 == item.TargetCategoryLevel3);
                        _db.AdWordsCustomTagLines.RemoveRange(del);
                        itemsRemoved++;
                    }
                }
                // add all category level that are not in ACTL
                foreach(var dc in dcsf)
                {
                    if (dctl.SingleOrDefault(x => x.TargetCategoryLevel1 == dc.CategoryLevel1
                                && x.TargetCategoryLevel2 == dc.CategoryLevel2
                                && x.TargetCategoryLevel3 == dc.CategoryLevel3) == null)
                    {
                        _db.AdWordsCustomTagLines.Add(new Domain.AdWordsCustomTagLine() {
                            ClientName = clientName, TargetCategoryLevel1 = dc.CategoryLevel1,
                            TargetCategoryLevel2 = dc.CategoryLevel2,
                            TargetCategoryLevel3 = dc.CategoryLevel3,
                            CreationDate = DateTime.Now
                        });
                        itemsAdded++;
                    }
                }
                await _db.SaveChangesAsync();

                result.ItemsAdded = itemsAdded;
                result.ItemsRemoved = itemsRemoved;
                return result;
            }
        }
    }
}
