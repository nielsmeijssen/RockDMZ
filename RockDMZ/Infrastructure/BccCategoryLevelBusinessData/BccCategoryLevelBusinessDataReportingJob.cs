using CsvHelper;
using Hangfire;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Infrastructure.BccCategoryLevelBusinessData
{
    public class BccCategoryLevelBusinessDataReportingJob
    {
        [AutomaticRetry(Attempts = 0)]
        public void RecurringDownload(int id, int retry, string localFeedLocation)
        {
            try
            {
                retry++;
                var dt = GetDatatable(id);

                var dateFirst = dt.LastDateDownloaded ?? dt.FirstDate;
                var dateLast = dt.LastDate ?? DateTime.MaxValue;
                //if (dateFirst < DateTime.Today && dateFirst < dateLast) // run more than once a day
                //{
                    // TODO Make sure central process updates SourceFeedItem table in db OverwriteBccSourceFeedData(@"c:\RockDMZ\Datatables\dce233ad-03f8-4ec2-8698-f8f534471769.csv");
                    // step 4: generate business feed
                    GenerateAdCustomizerFeed("S1000", dt.LocalFilePath.Replace(".csv", "-CLBD-MDA-Account.csv"));
                    GenerateAdCustomizerFeed("S1050", dt.LocalFilePath.Replace(".csv", "-CLBD-Multimedia-Account.csv"));
                    GenerateAdCustomizerFeed("S1020", dt.LocalFilePath.Replace(".csv", "-CLBD-Vision-Account.csv"));
                    GenerateAdCustomizerFeed("S1040", dt.LocalFilePath.Replace(".csv", "-CLBD-PortableTechnology-Account.csv"));
                    GenerateAdCustomizerFeed("S1030", dt.LocalFilePath.Replace(".csv", "-CLBD-Audio-Account.csv"));
                    GenerateAdCustomizerFeed("S1060", dt.LocalFilePath.Replace(".csv", "-CLBD-Accessories.csv"));
                    GenerateAdCustomizerFeed("S1010", dt.LocalFilePath.Replace(".csv", "-CLBD-SDA.csv"));
                    // update apidatatable
                    UpdateDatatable(id, DateTime.Now);
                //}


            }
            catch (Exception ex)
            {
                if (retry < 6)
                {
                    // sleep for 5 minutes
                    System.Threading.Thread.Sleep(300000);
                    RecurringDownload(id, retry, localFeedLocation);
                }
                else
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }
            }
        }

        private void GenerateAdCustomizerFeed(string accountId, string localFilePath)
        {
            var items = new List<List<string>>();
            var header = new List<string>();
            var hashtable = new HashSet<string>();

            var campaigns = new AdWordsCampaignStructure();
            campaigns.CampaignTemplates = new List<AdWordsCampaignTemplate>();

            var cGeneric = new AdWordsCampaignTemplate() { NameTemplate = "NB_Generiek_#CategoryLevel2SingularTC#" };
            cGeneric.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel2SingularLC#]" });
            cGeneric.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel2PluralLC#]" });
            campaigns.CampaignTemplates.Add(cGeneric);

            var cType = new AdWordsCampaignTemplate() { NameTemplate = "NB_Type_#CategoryLevel2SingularTC#" };
            cType.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "#CategoryLevel3ShortLC#" });
            campaigns.CampaignTemplates.Add(cGeneric);

            var cIntentH = new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-High_#CategoryLevel2SingularTC#" };
            cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "bestellen" });
            cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "kopen" });
            cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "online" });
            campaigns.CampaignTemplates.Add(cIntentH);

            var cIntentM = new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-Medium_#CategoryLevel2SingularTC#" };
            cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "aanbieding" });
            cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "goede" });
            cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "nieuw" });
            cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "vergelijk" });
            campaigns.CampaignTemplates.Add(cIntentM);

            var cBCC = new AdWordsCampaignTemplate() { NameTemplate = "NB_BCC_#CategoryLevel2SingularTC#" };
            cBCC.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "#CategoryLevel3SingularTC#" });
            // cBCC.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "#CategoryLevel3PluralTC#" });
            campaigns.CampaignTemplates.Add(cBCC);


            // create the join
            using (var db = new ToolsContext())
            {
                var input = (from i in db.BccSourceFeedItems
                             where i.AccountId == accountId
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
                                 i.PricePreviously
                             }).ToList();

                var customTagLines = db.AdWordsCustomTagLines.Where(x => x.ClientName == "BCC").ToList();

                // calculate fromPrice & numberofproducts for level2 and level3
                var l2FromPriceDict = new Dictionary<string, decimal>();
                var l2NoProductsDict = new Dictionary<string, int>();
                var l3FromPriceDict = new Dictionary<string, decimal>();
                var l3NoProductsDict = new Dictionary<string, int>();
                foreach (var i in input)
                {
                    var price = Convert.ToDecimal(i.Price, CultureInfo.CreateSpecificCulture("en-us"));
                    if (!l2FromPriceDict.Keys.Contains(i.CategoryLevel2))
                    {
                        l2FromPriceDict.Add(i.CategoryLevel2, price);
                    }
                    else
                    {
                        if (l2FromPriceDict[i.CategoryLevel2] > price )
                        {
                            l2FromPriceDict[i.CategoryLevel2] = price;
                        }
                    }
                    if (!l2NoProductsDict.Keys.Contains(i.CategoryLevel2))
                    {
                        l2NoProductsDict.Add(i.CategoryLevel2, 1);
                    }
                    else
                    {
                        l2NoProductsDict[i.CategoryLevel2] = l2NoProductsDict[i.CategoryLevel2] + 1;
                    }

                    // l3
                    if (!l3FromPriceDict.Keys.Contains(i.CategoryLevel3))
                    {
                        l3FromPriceDict.Add(i.CategoryLevel3, price);
                    }
                    else
                    {
                        if (l3FromPriceDict[i.CategoryLevel3] > price)
                        {
                            l3FromPriceDict[i.CategoryLevel3] = price;
                        }
                    }
                    if (!l3NoProductsDict.Keys.Contains(i.CategoryLevel3))
                    {
                        l3NoProductsDict.Add(i.CategoryLevel3, 1);
                    }
                    else
                    {
                        l3NoProductsDict[i.CategoryLevel3] = l3NoProductsDict[i.CategoryLevel3] + 1;
                    }
                }

                foreach (var r in input)
                {
                    var lac = new CategoryLevelBusinessDataItem();
                    // set header once
                    if (header.Count == 0) header = lac.Header;
                    
                    foreach(var ct in campaigns.CampaignTemplates)
                    {
                        var pt = r.CategoryLevel2.Replace(" en ", "-").Replace(" ", "-").Replace(" / ", "").Trim();
                        lac.CampaignName = ct.NameTemplate.Replace("#CategoryLevel2SingularTC#", pt);
                        lac.SetCategoryLevel1(r.CategoryLevel1);
                        lac.SetCategoryLevel2(r.CategoryLevel2);
                        lac.SetCategoryLevel3(r.CategoryLevel3);

                        foreach (var ag in ct.AdgroupTemplates)
                        {
                            var adgroupName = "";
                            adgroupName = ag.NameTemplate.Replace("#CategoryLevel3SingularLC#", lac.CategoryLevel3(false, "LC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel3PluralLC#", lac.CategoryLevel3(true, "LC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel3SingularTC#", lac.CategoryLevel3(false, "TC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel3PluralTC#", lac.CategoryLevel3(true, "TC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel3PluralTC#", lac.CategoryLevel3(true, "TC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel2PluralLC#", lac.CategoryLevel2(true, "LC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel2SingularTC#", lac.CategoryLevel2(false, "TC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel2SingularLC#", lac.CategoryLevel2(false, "LC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel2PluralTC#", lac.CategoryLevel2(true, "TC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel2PluralLC#", lac.CategoryLevel2(true, "LC"));
                            adgroupName = adgroupName.Replace("#CategoryLevel3ShortLC#", lac.CategoryLevel3(false, "LC").Replace(lac.CategoryLevel2(false, "LC"), "")).Trim();

                            lac.AdgroupName = adgroupName;
                            lac.CustomId = lac.CampaignName + "!" + lac.AdgroupName;

                            foreach (var ctl in customTagLines)
                            {
                                if (ctl.TargetCategoryLevel1 == r.CategoryLevel1)
                                {
                                    if (ctl.TargetCategoryLevel2 == null || ctl.TargetCategoryLevel2 == r.CategoryLevel2)
                                    {
                                        if (ctl.TargetCategoryLevel3 == null || ctl.TargetCategoryLevel3 == r.CategoryLevel3)
                                        {
                                            lac.BrandAwarenessLine30 = ctl.BrandAwarenessLine30;
                                            lac.BrandAwarenessLine50 = ctl.BrandAwarenessLine50;
                                            lac.BrandAwarenessLine80 = ctl.BrandAwarenessLine80;
                                            lac.PromoAwarenessLine30 = ctl.PromoAwarenessLine30;
                                            lac.PromoAwarenessLine50 = ctl.PromoAwarenessLine50;
                                            lac.PromoAwarenessLine80 = ctl.PromoAwarenessLine80;
                                            lac.ActivationLine30 = ctl.ActivationLine30;
                                            lac.ActivationLine50 = ctl.ActivationLine50;
                                            lac.ActivationLine80 = ctl.ActivationLine80;
                                        }
                                    }
                                }
                            }

                            // calculate fromPrice & numberofproducts for level2 and level3
                            lac.FromPriceLevel2PrettyPrice10 = lac.PrettifyPrice("€ " + Math.Round(l2FromPriceDict[r.CategoryLevel2],2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                            lac.FromPriceLevel2PrettyPrice10 = lac.ShortenPrice("€ " + Math.Round(l2FromPriceDict[r.CategoryLevel2], 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                            lac.FromPriceLevel3PrettyPrice10 = lac.PrettifyPrice("€ " + Math.Round(l3FromPriceDict[r.CategoryLevel3], 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                            lac.FromPriceLevel3PrettyPrice10 = lac.ShortenPrice("€ " + Math.Round(l3FromPriceDict[r.CategoryLevel3], 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                            lac.ProductsInCategoryLevel2 = l2NoProductsDict[r.CategoryLevel2].ToString();
                            lac.ProductsInCategoryLevel3 = l3NoProductsDict[r.CategoryLevel3].ToString();

                            lac.Level2FromPriceLine30 = lac.CategoryLevel2(true, "TC") + " v/a " + lac.FromPriceLevel2PrettyPrice10;
                            lac.Level3FromPriceLine30 = lac.CategoryLevel3(true, "TC") + " v/a " + lac.FromPriceLevel3PrettyPrice10;
                            lac.Level2ProductChoiceLine30 = "Kies uit " + lac.ProductsInCategoryLevel2 + " " + lac.CategoryLevel2(true, "LC");
                            lac.Level3ProductChoiceLine30 = "Kies uit " + lac.ProductsInCategoryLevel3 + " " + lac.CategoryLevel3(true, "LC");

                            // avoid duplicate entries
                            if (!hashtable.Contains(lac.CustomId))
                            {
                                hashtable.Add(lac.CustomId);
                                items.Add(lac.Row);
                            }
                        }

                    }
                }
            }
            // store feed
            AddToFile(items, localFilePath, header, false);
        }

        private ApiDatatable GetDatatable(int id)
        {
            using (var db = new ToolsContext())
            {
                var dt = db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(x => x.Id.Equals(id));
                return dt;
            }
        }

        private void UpdateDatatable(int id, DateTime lastDateDownloaded)
        {
            using (var db = new ToolsContext())
            {
                var dt = db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(x => x.Id.Equals(id));

                dt.LastDateDownloaded = lastDateDownloaded;

                db.SaveChanges();
            }
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
