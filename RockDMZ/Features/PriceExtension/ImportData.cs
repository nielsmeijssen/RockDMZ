using AutoMapper;
using CsvHelper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Rendering;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Features.PriceExtension
{
    public class ImportData
    {
        public class Query : IRequest<Model>
        {
            public int? Id { get; set; }
        }

        public class Model
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

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class QueryHandler : IAsyncRequestHandler<Query, Model>
        {
            private readonly ToolsContext _db;
            private string connString;

            public QueryHandler(ToolsContext db)
            {
                _db = db;

                
            }

            public async Task<Model> Handle(Query message)
            {
                var rtn = await _db.PriceExtensionProjects.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Model>();

                // delete all data from priceextensionproductfeeds for this project
                using (var db = new ToolsContext())
                {
                    db.Database.ExecuteSqlCommand("DELETE FROM [PriceExtensionProductFeed] WHERE PriceExtensionProjectId = " + message.Id);
                }
                // import product feed into priceextensionproductfeeds
                // read the source file and add each record to the DB
                using (var sr = new StreamReader(rtn.ProductFeedLocation))
                {
                    var reader = new CsvReader(sr);
                    
                    reader.Configuration.RegisterClassMap<PriceExtensionProductFeedCsvMap>();

                    var records = reader.GetRecords<PriceExtensionProductFeed>().ToList();

                    records.ForEach(x => x.PriceExtensionProjectId = (int)message.Id);
                    records.ForEach(x => x.LinkLevel2 = GetBccLink(x.Link, x.Brand, 2));
                    records.ForEach(x => x.LinkLevel3 = GetBccLink(x.Link, x.Brand, 3));

                    foreach (IEnumerable<PriceExtensionProductFeed> batch in Partition(records, 1000))
                    {
                        InsertPriceExtensionProductFeedItems(batch.ToList());
                    }

                }

                // delete all data from priceextensionproductperformance for this project
                using (var db = new ToolsContext(connString))
                {
                    db.Database.ExecuteSqlCommand("DELETE FROM [PriceExtensionProductPerformance] WHERE PriceExtensionProjectId = " + message.Id);
                }

                // import product feed into priceextensionproductperformances
                // read the source file and add each record to the DB
                using (var sr = new StreamReader(rtn.ProductPerformanceFeedLocation))
                {
                    var reader = new CsvReader(sr);
                    reader.Configuration.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");

                    reader.Configuration.RegisterClassMap<PriceExtensionProductPerformanceCsvMap>();

                    var records = reader.GetRecords<PriceExtensionProductPerformance>().ToList();

                    records.ForEach(x => x.PriceExtensionProjectId = (int)message.Id);

                    foreach (IEnumerable<PriceExtensionProductPerformance> batch in Partition(records, 1000))
                    {
                        InsertPriceExtensionProductPerformanceItems(batch.ToList());
                    }

                }




                return rtn;
            }

            private string GetBccLink(string link, string brand, int level)
            {
                var index = level == 2 ? 3 : 2;
                // verwijder laatste twee path onderdelen + query
                var linkArray = link.Split(new[] { '/' }).ToList();
                linkArray = linkArray.Take(linkArray.Count - index).ToList();
                var rtn = String.Join("/", linkArray) + @"/" + brand.ToLower();
                return rtn;
            }

            private void InsertPriceExtensionProductFeedItems(List<PriceExtensionProductFeed> records)
            {
                using (var db = new ToolsContext(connString))
                {
                    db.Configuration.AutoDetectChangesEnabled = false;
                    db.Configuration.ValidateOnSaveEnabled = false;

                    db.PriceExtensionProductFeeds.AddRange(records);
                    db.SaveChanges();
                }
            }

            private void InsertPriceExtensionProductPerformanceItems(List<PriceExtensionProductPerformance> records)
            {
                using (var db = new ToolsContext(connString))
                {
                    db.Configuration.AutoDetectChangesEnabled = false;
                    db.Configuration.ValidateOnSaveEnabled = false;

                    db.PriceExtensionProductPerformances.AddRange(records);
                    db.SaveChanges();
                }
            }

            public static IEnumerable<List<T>> Partition<T>(List<T> locations, int nSize = 30)
            {
                for (int i = 0; i < locations.Count; i += nSize)
                {
                    yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
                }
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
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
            }
        }
    }
}
