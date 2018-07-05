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

namespace RockDMZ.Features.PriceExtension
{
    public class Generate
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; } // PriceExtensionProject.Id
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
                var tooFewItems = 0;

                List<RockDMZ.Domain.PriceExtension> extensions = new List<RockDMZ.Domain.PriceExtension>();

                var campaigns = new AdWordsCampaignStructure();
                campaigns.CampaignTemplates = new List<AdWordsCampaignTemplate>();
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Generiek_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-High_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-Medium_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_BCC_#ProductType#" });

                // get a list of distinct product types in the product feed
                var productTypes = _db.PriceExtensionProductFeeds.Select(x => x.ProductType).Distinct();

                foreach(var pt in productTypes)
                {
                    var campaignName = pt.Replace(" en ", "-").Replace(" ", "-");
                    
                    foreach(var c in campaigns.CampaignTemplates)
                    {
                        var pe = new RockDMZ.Domain.PriceExtension();
                        // pe.Action = "SET";
                        // pe.ItemId = "";

                        pe.Campaign = c.NameTemplate.Replace("#ProductType#", campaignName);
                        pe.Adgroup = "";
                        pe.CustomId = pe.Campaign + "#" + pe.Adgroup;
                        pe.Type = "Merken";
                        pe.PriceQualifier = "Vanaf";
                        pe.Language = "nl";
                        // pe.TrackingTemplate = "";
                        pe.Items = new List<Item>();

                        // get the brands for this producttype
                        var brands = (from x in _db.PriceExtensionProductFeeds
                                      group x by new { x.ItemHeader, x.ItemDescription, x.ItemPrice, x.ItemPriceUnit, x.LinkLevel2, x.PriceExtensionProjectId, x.ProductType, x.FromPriceLevel2, x.Account } into p
                                      where p.Key.ProductType == pt where p.Key.ItemDescription != "" where p.Key.ItemHeader != "" where p.Key.ItemHeader != null where p.Key.ItemDescription != null
                                      select new Domain.Item() { Header = p.Key.ItemHeader, Description = p.Key.ItemDescription, Price = p.Key.ItemPrice, PriceUnit = p.Key.ItemPriceUnit, FinalUrl = p.Key.LinkLevel2, FromPrice = p.Key.FromPriceLevel2, Account = p.Key.Account }
                                      ).ToList();
                        if (brands.Count() < 3)
                        {
                            tooFewItems++;
                            break;
                        }

                        var counter = 0;

                        foreach(var b in brands)
                        {
                            counter++;
                            if (counter > 8) break;

                            // clean up itemheader & itemprice
                            b.Price = ConvertToPriceExtensionPrice(Math.Round(Convert.ToDouble(b.FromPrice), 2).ToString());
                            b.Header = PrettifyString(b.Header, true);
                            b.PriceUnit = pe.PriceQualifier;

                            b.Index = counter;
                            pe.Items.Add(b);

                        }

                        extensions.Add(pe);
                    }
                }

                var r = GeneratePreviewReport(extensions);
                var rtn = new Command();
                rtn.Report = r;
                rtn.Id = Convert.ToInt32(message.Id);
                rtn.TooFewItems = tooFewItems;
                return rtn;
            }

            public List<List<string>> GeneratePreviewReport(List<Domain.PriceExtension> extensions)
            {
                var rtn = new List<List<string>>();
                // get headers
                var headers = new List<string>();

                headers.Add("Account");

                if (!String.IsNullOrEmpty(extensions[0].Action)) headers.Add("Action");
                headers.Add("Campaign");
                if (!String.IsNullOrEmpty(extensions[0].Adgroup)) headers.Add("Ad group");
                headers.Add("Language");
                headers.Add("Type");
                headers.Add("Price qualifier");
                //headers.Add("Custom ID");

                for(int i=1;i<9;i++)
                {
                    headers.Add("Item "+ i + " header");
                    headers.Add("Item " + i + " final URL");
                    headers.Add("Item " + i + " price");
                    headers.Add("Item "+ i + " description");
                }

                rtn.Add(headers);

                foreach(var e in extensions)
                {
                    var item = new List<string>();

                    item.Add(e.Items[0].Account);

                    if (headers.Contains("Action")) item.Add(e.Action);
                    item.Add(e.Campaign);
                    if (headers.Contains("Ad group")) item.Add(e.Adgroup);
                    item.Add(e.Language);
                    item.Add(e.Type);
                    item.Add(e.PriceQualifier);
                    //item.Add(e.CustomId);

                    for(int i=0;i<e.Items.Count;i++)
                    {
                        item.Add(e.Items[i].Header);
                        item.Add(e.Items[i].FinalUrl);
                        item.Add(e.Items[i].Price);
                        item.Add(e.Items[i].Description);
                    }

                    for(int j=0;j<(8-e.Items.Count);j++)
                    {
                        item.Add("");
                        item.Add("");
                        item.Add("");
                        item.Add("");
                    }

                    rtn.Add(item);
                }

                return rtn;
            }

            public string ConvertToPriceExtensionPrice(string price)
            {
                var rtn = "";
                if (price.IndexOfAny(new[] { '.', ',' }) == -1) rtn = price + ",00 EUR";

                else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') > price.IndexOf(',')) // US format 1,000.95
                {
                    rtn = price.Replace(",", "").Replace(".", ",") + " EUR";
                }

                else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') < price.IndexOf(',')) // EU format 1.000,95
                {
                    rtn = price.Replace(".", "") + " EUR";
                }

                else if (price.IndexOf('.') != -1)
                {
                    rtn = price.Replace(".", ",") + " EUR";
                }

                else rtn = price + " EUR";

                return rtn;
            }

            public string PrettifyString(string s, bool capitalizeAcronyms = false)
            {
                if (String.IsNullOrWhiteSpace(s)) return "";
                string rtn = "";
                var l = s.Split(new[] { ' ' });
                foreach (string word in l)
                {
                    switch (word.Length)
                    {
                        case 0:
                            break;
                        case 1:
                            rtn += word.ToUpper(); // no space after a one-letter word
                            break;
                        case 2:
                            if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                            else rtn += word + " ";
                            break;
                        case 3:
                            if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                            else rtn += word + " ";
                            break;
                        default:
                            // is it a type (has both letters and numbers)
                            if (word.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }) != -1 && (Regex.Matches(word, @"[a-zA-Z]").Count > 0))
                            {
                                // if type has lower case and upper case letters -> leave as is iQ900
                                if (Regex.Matches(word, @"[A-Z]").Count > 0 && Regex.Matches(word, @"[a-z]").Count > 0)
                                {
                                    rtn += word + " ";
                                }
                                else
                                {
                                    rtn += word.ToUpper() + " ";
                                }

                            }
                            else // not a type -> capitalize first letter
                            {
                                var newWord = word.ToLower();
                                rtn += newWord.First().ToString().ToUpper() + newWord.Substring(1) + " ";
                            }
                            break;
                    }
                }
                return rtn.Trim();
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public List<List<string>> Report { get; set; }

            public int TooFewItems { get; internal set; }
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
                var peProject = _db.PriceExtensionProjects.Where(i => i.Id == message.Id).SingleOrDefault();
                if (peProject == null) throw new Exception("Id missing");
                // get extensions
                var extensions = GetLevel2Extensions();
                var headers = new List<string>();
                // for each account store the report as a business data feed with custom ID
                var reports = GenerateReport(extensions, out headers, true);
                foreach(var report in reports)
                {
                    AddToFile(report.Value, peProject.PriceExtensionFeedDestinationLocation.Replace(".csv", "-" + report.Key.ToUpper() + "-level2.csv"), headers, false);
                }
                // for each account store the report as a editor feed without custom ID
                reports = GenerateReport(extensions, out headers, false);
                foreach (var report in reports)
                {
                    AddToFile(report.Value, peProject.PriceExtensionFeedDestinationLocation.Replace(".csv", "-" + report.Key.ToUpper() + "-level2-editorversion.csv"), headers, false);
                }
            }

            public Dictionary<string,List<List<string>>> GenerateReport(List<Domain.PriceExtension> extensions, out List<string> headers, bool includeCustomId)
            {
                var rtn = new Dictionary<string, List<List<string>>>();

                headers = new List<string>();

                if (!String.IsNullOrEmpty(extensions[0].Action)) headers.Add("Action");
                headers.Add("Campaign");
                if (!String.IsNullOrEmpty(extensions[0].Adgroup)) headers.Add("Ad group");
                headers.Add("Language");
                headers.Add("Type");
                headers.Add("Price qualifier");
                //if (includeCustomId) headers.Add("Item-ID");

                for (int i = 1; i < 9; i++)
                {
                    headers.Add("Item " + i + " header");
                    headers.Add("Item " + i + " final URL");
                    headers.Add("Item " + i + " price");
                    headers.Add("Item " + i + " description");
                }

                foreach (var e in extensions)
                {
                    var account = e.Items[0].Account;
                    var item = new List<string>();

                    if (headers.Contains("Action")) item.Add(e.Action);
                    item.Add(e.Campaign);
                    if (headers.Contains("Ad group")) item.Add(e.Adgroup);
                    item.Add(e.Language);
                    item.Add(e.Type);
                    item.Add(e.PriceQualifier);
                    //if (includeCustomId) item.Add(e.CustomId);

                    for (int i = 0; i < e.Items.Count; i++)
                    {
                        item.Add(e.Items[i].Header);
                        item.Add(e.Items[i].FinalUrl);
                        item.Add(e.Items[i].Price);
                        item.Add(e.Items[i].Description);
                    }

                    for (int j = 0; j < (8 - e.Items.Count); j++)
                    {
                        item.Add("");
                        item.Add("");
                        item.Add("");
                        item.Add("");
                    }

                    if (!rtn.ContainsKey(account))
                    {
                        rtn.Add(account, new List<List<string>>() { item });
                    }
                    else
                    {
                        rtn[account].Add(item);
                    }
                }

                return rtn;
            }

            public List<Domain.PriceExtension> GetLevel2Extensions()
            {
                List<RockDMZ.Domain.PriceExtension> extensions = new List<RockDMZ.Domain.PriceExtension>();

                var campaigns = new AdWordsCampaignStructure();
                campaigns.CampaignTemplates = new List<AdWordsCampaignTemplate>();
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Generiek_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-High_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-Medium_#ProductType#" });
                campaigns.CampaignTemplates.Add(new AdWordsCampaignTemplate() { NameTemplate = "NB_BCC_#ProductType#" });

                // get a list of distinct product types in the product feed
                var productTypes = _db.PriceExtensionProductFeeds.Select(x => x.ProductType).Distinct();

                foreach (var pt in productTypes)
                {
                    var campaignName = pt.Replace(" en ", "-").Replace(" ", "-");

                    foreach (var c in campaigns.CampaignTemplates)
                    {
                        var pe = new RockDMZ.Domain.PriceExtension();
                        pe.Action = "ADD";
                        pe.Campaign = c.NameTemplate.Replace("#ProductType#", campaignName);
                        pe.Adgroup = "";
                        pe.CustomId = pe.Campaign + "#" + pe.Adgroup;
                        pe.ItemId = pe.CustomId;
                        pe.Type = "Merken";
                        pe.PriceQualifier = "Vanaf";
                        pe.Language = "nl";
                        // pe.TrackingTemplate = "";
                        pe.Items = new List<Item>();

                        // get the brands for this producttype
                        var brands = (from x in _db.PriceExtensionProductFeeds
                                      group x by new { x.ItemHeader, x.ItemDescription, x.ItemPrice, x.ItemPriceUnit, x.LinkLevel2, x.PriceExtensionProjectId, x.ProductType, x.FromPriceLevel2, x.Account } into p
                                      where p.Key.ProductType == pt
                                      where p.Key.ItemDescription != ""
                                      where p.Key.ItemHeader != ""
                                      where p.Key.ItemHeader != null
                                      where p.Key.ItemDescription != null
                                      select new Domain.Item() { Header = p.Key.ItemHeader, Description = p.Key.ItemDescription, Price = p.Key.ItemPrice, PriceUnit = p.Key.ItemPriceUnit, FinalUrl = p.Key.LinkLevel2, FromPrice = p.Key.FromPriceLevel2, Account = p.Key.Account }
                                      ).ToList();
                        if (brands.Count() < 3)
                        {
                            break;
                        }

                        var counter = 0;

                        foreach (var b in brands)
                        {
                            counter++;
                            if (counter > 8) break;

                            // clean up itemheader & itemprice
                            b.Price = ConvertToPriceExtensionPrice(Math.Round(Convert.ToDouble(b.FromPrice), 2).ToString());
                            b.Header = PrettifyString(b.Header, true);
                            b.PriceUnit = pe.PriceQualifier;

                            b.Index = counter;
                            if (!pe.Items.Exists(x => x.Header == b.Header)) pe.Items.Add(b);

                        }

                        extensions.Add(pe);
                    }
                }
                return extensions;
            }

            public string ConvertToPriceExtensionPrice(string price)
            {
                var rtn = "";
                if (price.IndexOfAny(new[] { '.', ',' }) == -1) rtn = price + ",00 EUR";

                else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') > price.IndexOf(',')) // US format 1,000.95
                {
                    rtn = price.Replace(",", "").Replace(".", ",") + " EUR";
                }

                else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') < price.IndexOf(',')) // EU format 1.000,95
                {
                    rtn = price.Replace(".", "") + " EUR";
                }

                else if (price.IndexOf('.') != -1)
                {
                    rtn = price.Replace(".", ",") + " EUR";
                }

                else rtn = price + " EUR";

                return rtn;
            }

            public string PrettifyString(string s, bool capitalizeAcronyms = false)
            {
                if (String.IsNullOrWhiteSpace(s)) return "";
                string rtn = "";
                var l = s.Split(new[] { ' ' });
                foreach (string word in l)
                {
                    switch (word.Length)
                    {
                        case 0:
                            break;
                        case 1:
                            rtn += word.ToUpper(); // no space after a one-letter word
                            break;
                        case 2:
                            if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                            else rtn += word + " ";
                            break;
                        case 3:
                            if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                            else rtn += word + " ";
                            break;
                        default:
                            // is it a type (has both letters and numbers)
                            if (word.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }) != -1 && (Regex.Matches(word, @"[a-zA-Z]").Count > 0))
                            {
                                // if type has lower case and upper case letters -> leave as is iQ900
                                if (Regex.Matches(word, @"[A-Z]").Count > 0 && Regex.Matches(word, @"[a-z]").Count > 0)
                                {
                                    rtn += word + " ";
                                }
                                else
                                {
                                    rtn += word.ToUpper() + " ";
                                }

                            }
                            else // not a type -> capitalize first letter
                            {
                                var newWord = word.ToLower();
                                rtn += newWord.First().ToString().ToUpper() + newWord.Substring(1) + " ";
                            }
                            break;
                    }
                }
                return rtn.Trim();
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
