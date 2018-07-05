using CsvHelper;
using Hangfire;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Infrastructure.BccProductLevelBusinessData
{
    public class BccProductLevelBusinessDataReportingJob
    {
        [AutomaticRetry(Attempts = 0)]
        public void RecurringDownload(int id, int retry, string localFeedDirectory)
        {
            try
            {
                retry++;
                var dt = GetDatatable(id);

                var dateFirst = dt.LastDateDownloaded ?? dt.FirstDate;
                var dateLast = dt.LastDate ?? DateTime.MaxValue;
                //if (dateFirst < DateTime.Today && dateFirst < dateLast)   // generate several times per day
                //{
                    // TODO Make sure central process updates SourceFeedItem table in db OverwriteBccSourceFeedData(@"c:\RockDMZ\Datatables\dce233ad-03f8-4ec2-8698-f8f534471769.csv");
                    // step 4: generate business feed
                    GenerateAdCustomizerFeed("S1000", dt.LocalFilePath.Replace(".csv", "-PLBD-MDA-Account.csv"));
                    GenerateAdCustomizerFeed("S1050", dt.LocalFilePath.Replace(".csv", "-PLBD-Multimedia-Account.csv"));
                    GenerateAdCustomizerFeed("S1020", dt.LocalFilePath.Replace(".csv", "-PLBD-Vision-Account.csv"));
                    GenerateAdCustomizerFeed("S1040", dt.LocalFilePath.Replace(".csv", "-PLBD-PortableTechnology-Account.csv"));
                    GenerateAdCustomizerFeed("S1030", dt.LocalFilePath.Replace(".csv", "-PLBD-Audio-Account.csv"));
                    GenerateAdCustomizerFeed("S1060", dt.LocalFilePath.Replace(".csv", "-PLBD-Accessories.csv"));
                    GenerateAdCustomizerFeed("S1010", dt.LocalFilePath.Replace(".csv", "-PLBD-SDA.csv"));
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
                    RecurringDownload(id, retry, localFeedDirectory);
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


                foreach (var r in input)
                {
                    var lac = new ProductLevelBusinessDataItem();
                    // set header once
                    if (header.Count == 0) header = lac.Header;
                    // construct campaignName
                    lac.CampaignName = "NB_Producten_" + r.CategoryLevel2.Trim();
                    // construct adgroupName
                    lac.AdgroupName = r.ProductName.Trim();
                    lac.CustomId = lac.CampaignName + "!" + lac.AdgroupName;
                    lac.Price = r.Price;
                    lac.StockQuantity = r.StockQuantity.ToString();
                    lac.Brand = lac.PrettifyString(r.Brand);
                    lac.ProductName = r.ProductName;
                    lac.Type = r.Type;
                    lac.SetCategoryLevel1(r.CategoryLevel1);
                    lac.SetCategoryLevel2(r.CategoryLevel2);
                    lac.SetCategoryLevel3(r.CategoryLevel3);
                    lac.SourceFeedPromoLine = r.SourceFeedPromoLine;

                    var price = String.IsNullOrWhiteSpace(r.Price) ? (decimal)0 : Convert.ToDecimal(r.Price, CultureInfo.CreateSpecificCulture("en-us"));
                    decimal priceOld = String.IsNullOrWhiteSpace(r.PricePreviously) ? (decimal)0 : Convert.ToDecimal(r.PricePreviously, CultureInfo.CreateSpecificCulture("en-us"));

                    if (priceOld != (decimal)0 && price < priceOld)
                    {
                        var prettyOldPrice = lac.PrettifyPrice("€ " + Math.Round(priceOld, 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                        var prettyPriceNew = lac.PrettifyPrice("€ " + Math.Round(price, 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                        lac.DiscountPercentage3 = Math.Round(((priceOld - price) / priceOld)*100).ToString() + "%";
                        lac.DiscountAmountShort4 = lac.ShortenPrice("€ " + Math.Round(priceOld - price, 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                        lac.DiscountAmountPretty8 = lac.PrettifyPrice("€ " + Math.Round(priceOld - price, 2).ToString(CultureInfo.CreateSpecificCulture("nl-nl")));
                        lac.DiscountLine30 = "Tijdelijk " + lac.DiscountAmountPretty8 + " korting!";
                        lac.DiscountLine50 = "Nu bij BCC van " + prettyOldPrice + " voor " + prettyPriceNew;
                    }

                    foreach(var ctl in customTagLines)
                    {
                        if (ctl.TargetSourceFeedPromoLine == r.SourceFeedPromoLine && !String.IsNullOrWhiteSpace(r.SourceFeedPromoLine))
                        {
                            lac.PromoLine30 = ctl.ProductLevelPromoLine30;
                            lac.PromoLine50 = ctl.ProductLevelPromoLine50;
                            lac.PromoLine80 = ctl.ProductLevelPromoLine80;
                            break;
                        }
                    }

                    // avoid duplicate entries
                    if (!hashtable.Contains(lac.CustomId))
                    {
                        hashtable.Add(lac.CustomId);
                        items.Add(lac.Row);
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
