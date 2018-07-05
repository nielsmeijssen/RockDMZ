using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using CsvHelper;
using System.Globalization;

namespace RockDMZ.Features.PromotionExtension
{
    public class Generate
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; } // PromotionExtensionProject.Id
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
                var peProject = _db.PromotionExtensionProjects.SingleOrDefault(x => x.Id == message.Id);

                var r = GeneratePreviewReport((int)message.Id);
                var rtn = new Command();
                rtn.Report = r;
                rtn.Id = Convert.ToInt32(message.Id);
                return rtn;
            }

            private string GeneratePromotionTarget(string p, string b, string t, string cl2, string cl3)
            {
                string rtn = p;
                var bd = new PromotionFeedDataItem();

                if (rtn.Length > 20) // try brand + cl3
                {
                    rtn = b + " " + cl3;
                }

                if (rtn.Length > 20) // try brand + cl2
                {
                    rtn = b + " " + cl2;
                }

                if (rtn.Length > 20) // try brand + type
                {
                    rtn = b + " " + t;
                }

                if (rtn.Length > 20) // ignore
                {
                    rtn = "";
                }

                return bd.PrettifyString(rtn);
            }

            public List<List<string>> GeneratePreviewReport(int id)
            {
                var items = new List<List<string>>();
                items.Add(new PromotionFeedDataItem().Header);
                // create the join
                using (var db = new ToolsContext())
                {
                    var input = (from i in db.BccSourceFeedItems
                                 // where i.AccountId == accountId
                                 select new
                                 {
                                     i.ClientProductId,
                                     i.CategoryLevel1,
                                     i.CategoryLevel2,
                                     i.CategoryLevel3,
                                     i.Brand,
                                     i.Price,
                                     i.ProductName,
                                     i.Type,
                                     i.AccountId,
                                     i.StockQuantity,
                                     i.SourceFeedPromoLine,
                                     i.PricePreviously,
                                     i.Link
                                 }).ToList();

                    var peProject = db.PromotionExtensionProjects.SingleOrDefault(x => x.Id == id);

                    foreach (var r in input)
                    {
                        var lac = new PromotionFeedDataItem();
                        
                        lac.FinalUrl = r.Link;
                        lac.SetEndDate(DateTime.Now.AddDays(peProject.DefaultPromoDurationInDays));
                        lac.SetStartDate(DateTime.Now);
                        lac.Language = "nl";
                        lac.MinimumOrderValue = "";
                        lac.Occasion = "";
                        lac.PromotionDiscountModifier = "";
                        var promotionText = GeneratePromotionTarget(r.ProductName, r.Brand, r.Type, r.CategoryLevel2, r.CategoryLevel3);

                        if (promotionText == "") promotionText = "-- TOO LONG --  " + r.ProductName;

                        if ("AEG,BEKO,LENOVO,LG,LIEBHERR,BABYLISS,NESPRESSO".Split(new[] { ',' }).Contains(r.Brand.ToUpper())) promotionText = "-- TRADEMARK ISSUE --  " + r.ProductName;

                        lac.PromotionText = promotionText;

                        var price = String.IsNullOrWhiteSpace(r.Price) ? (decimal)0 : Convert.ToDecimal(r.Price, CultureInfo.CreateSpecificCulture("en-us"));
                        decimal priceOld = String.IsNullOrWhiteSpace(r.PricePreviously) ? (decimal)0 : Convert.ToDecimal(r.PricePreviously, CultureInfo.CreateSpecificCulture("en-us"));
                        if (priceOld == (decimal)0 || price >= priceOld) continue;

                        var discountPercentage = Math.Round(((priceOld - price) / priceOld) * 100);
                        var discountAmount = Math.Round(priceOld - price, 2);

                        lac.PromotionMoneyAmoutOff = "";
                        if (peProject.UseAmounts)
                        {
                            if (Convert.ToInt32(discountAmount) >= peProject.MinimumAmout || discountPercentage >= peProject.OrMinimumAmountPercentage)
                            {
                                lac.PromotionMoneyAmoutOff = lac.ConvertToPromotionExtensionPrice(discountAmount.ToString());
                                items.Add(lac.Row);
                            }
                        }
                        lac.PromotionPercentOff = "";
                        if (peProject.UsePercentages && discountPercentage >= peProject.MinimumPercentage)
                        {
                            lac.PromotionPercentOff = discountPercentage.ToString();
                            items.Add(lac.Row);
                        }
                    }
                }

                return items;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public List<List<string>> Report { get; set; }
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
                var peProject = _db.PromotionExtensionProjects.Where(i => i.Id == message.Id).SingleOrDefault();
                if (peProject == null) throw new Exception("Id missing");
                // get extensions
                var headers = new List<string>();
                // for each account store the report as a business data feed with custom ID
                GenerateReport(null, "Corporate", peProject); // if accountid = null => include all (e.g. for corporate account)
                GenerateReport("S1000", "MDA", peProject);
                GenerateReport("S1010", "SDA", peProject);
                GenerateReport("S1020", "Vision", peProject);
                GenerateReport("S1030", "Audio", peProject);
                GenerateReport("S1040", "PortableTechnology", peProject);
                GenerateReport("S1050", "MultiMedia", peProject);
                GenerateReport("S1060", "Accessories", peProject);
            }

            public void GenerateReport(string accountId, string accountName, PromotionExtensionProject pep)
            {
                var items = new List<List<string>>();
                var headers = new PromotionFeedDataItem().Header;
                // create the join
                using (var db = new ToolsContext())
                {
                    var input = (from i in db.BccSourceFeedItems
                                     // where i.AccountId == accountId
                                 select new
                                 {
                                     i.ClientProductId,
                                     i.CategoryLevel1,
                                     i.CategoryLevel2,
                                     i.CategoryLevel3,
                                     i.Brand,
                                     i.Price,
                                     i.ProductName,
                                     i.Type,
                                     i.AccountId,
                                     i.StockQuantity,
                                     i.SourceFeedPromoLine,
                                     i.PricePreviously,
                                     i.Link
                                 }).ToList();

                    var peProject = pep;

                    foreach (var r in input)
                    {
                        if (accountId != null && r.AccountId != accountId) continue;
                        int stock = 0;
                        if (!Int32.TryParse(r.StockQuantity, out stock) || stock == 0) continue;

                        if ("AEG,BEKO,LENOVO,LG,LIEBHERR,BABYLISS,NESPRESSO".Split(new[] { ',' }).Contains(r.Brand.ToUpper())) continue;

                        var lac = new PromotionFeedDataItem();
                        
                        lac.FinalUrl = r.Link;
                        lac.SetEndDate(DateTime.Now.AddDays(peProject.DefaultPromoDurationInDays));
                        lac.SetStartDate(DateTime.Now);
                        lac.Language = "nl";
                        lac.MinimumOrderValue = "";
                        lac.Occasion = "";
                        lac.PromotionDiscountModifier = "";
                        var promotionText = GeneratePromotionTarget(r.ProductName, r.Brand, r.Type, r.CategoryLevel2, r.CategoryLevel3);

                        if (promotionText == "") continue;

                        lac.PromotionText = promotionText;

                        var price = String.IsNullOrWhiteSpace(r.Price) ? (decimal)0 : Convert.ToDecimal(r.Price, CultureInfo.CreateSpecificCulture("en-us"));
                        decimal priceOld = String.IsNullOrWhiteSpace(r.PricePreviously) ? (decimal)0 : Convert.ToDecimal(r.PricePreviously, CultureInfo.CreateSpecificCulture("en-us"));
                        if (priceOld == (decimal)0 || price >= priceOld) continue;

                        var discountPercentage = Math.Round(((priceOld - price) / priceOld) * 100);
                        var discountAmount = Math.Round(priceOld - price, 2);

                        lac.PromotionMoneyAmoutOff = "";
                        if (peProject.UseAmounts)
                        {
                            if (Convert.ToInt32(discountAmount) >= peProject.MinimumAmout || discountPercentage >= peProject.OrMinimumAmountPercentage)
                            {
                                lac.PromotionMoneyAmoutOff = lac.ConvertToPromotionExtensionPrice(discountAmount.ToString());
                                items.Add(lac.Row);
                            }
                        }
                        lac.PromotionPercentOff = "";
                        if (peProject.UsePercentages && discountPercentage >= peProject.MinimumPercentage)
                        {
                            lac.PromotionMoneyAmoutOff = "";
                            lac.PromotionPercentOff = Convert.ToInt32(discountPercentage).ToString() + ",000,000";
                            items.Add(lac.Row);
                        }
                    }
                }

                AddToFile(items, pep.PromotionExtensionFeedDestinationLocation.Replace(".csv", "-" + accountName.ToUpper() + ".csv"), headers, false);
            }

            private string GeneratePromotionTarget(string p, string b, string t, string cl2, string cl3)
            {
                string rtn = p;
                var bd = new PromotionFeedDataItem();

                if (rtn.Length > 20) // try brand + cl3
                {
                    rtn = b + " " + cl3;
                }

                if (rtn.Length > 20) // try brand + cl2
                {
                    rtn = b + " " + cl2;
                }

                if (rtn.Length > 20) // try brand + type
                {
                    rtn = b + " " + t;
                }

                if (rtn.Length > 20) // ignore
                {
                    rtn = "";
                }

                return bd.PrettifyString(rtn);
            }

            private void AddToFile(List<List<string>> data, string fileLocation, List<string> headers, bool append)
            {
                try
                {
                    if (headers == null || headers.Count == 0)
                    {
                        return;
                    }
                    if (append)
                    {
                        if (!File.Exists(fileLocation))
                        {
                            using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                            using (StreamWriter sw = new StreamWriter(aFile))
                            {
                                var csv = new CsvWriter(sw);
                                //csv.Configuration.QuoteAllFields = true;
                                foreach (var header in headers)
                                {
                                    csv.WriteField(header);
                                }
                                csv.NextRecord();
                                foreach (var row in data)
                                {
                                    foreach (var record in row)
                                    {
                                        csv.WriteField(record);
                                    }
                                    csv.NextRecord();
                                }
                                data = null;
                            }
                        }
                        else
                        {
                            using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                            using (StreamWriter sw = new StreamWriter(aFile))
                            {
                                var csv = new CsvWriter(sw);
                                //csv.Configuration.QuoteAllFields = true;
                                foreach (var row in data)
                                {
                                    foreach (var record in row)
                                    {
                                        csv.WriteField(record);
                                    }
                                    csv.NextRecord();
                                }
                                data = null;
                            }
                        }
                    }
                    else
                    {
                        using (FileStream aFile = new FileStream(fileLocation, FileMode.Create, FileAccess.Write))
                        using (StreamWriter sw = new StreamWriter(aFile))
                        {
                            var csv = new CsvWriter(sw);
                            //csv.Configuration.QuoteAllFields = true;
                            foreach (var header in headers)
                            {
                                csv.WriteField(header);
                            }
                            csv.NextRecord();
                            foreach (var row in data)
                            {
                                foreach (var record in row)
                                {
                                    csv.WriteField(record);
                                }
                                csv.NextRecord();
                            }
                            data = null;
                        }
                    }


                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }

            }
        }

    }
}
