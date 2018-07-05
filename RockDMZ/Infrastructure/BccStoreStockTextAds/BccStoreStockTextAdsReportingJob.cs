using CsvHelper;
using Hangfire;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Infrastructure.BccStoreStockTextAds
{
    public class BccStoreStockTextAdsReportingJob
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
                if (dateFirst < DateTime.Today && dateFirst < dateLast)
                {
                    // step 1: overwrite store data in db table: StoreData
                    OverwriteStoreData(localFeedLocation + "bccstoredatafile.csv", "BCC");

                    // step 2: overwrite bronfeed data in db table: BccSourceFeedItem
                    OverwriteBccSourceFeedData(localFeedLocation + "dce233ad-03f8-4ec2-8698-f8f534471769.csv");

                    // step 3: overwrite store stock data in db table:  BccStoreStockLatest
                    OverwriteBccStoreStockData(localFeedLocation + "b6b187b2-7295-4de4-83ac-1e3fa466a5ef.csv");

                    // update apidatatable
                    UpdateDatatable(id, DateTime.Now);
                }

                // step 4: generate business feed
                GenerateAdCustomizerFeed("S1000", dt.LocalFilePath.Replace(".csv", "-MDA-Account.csv"));
                GenerateAdCustomizerFeed("S1050", dt.LocalFilePath.Replace(".csv", "-Multimedia-Account.csv"));
                GenerateAdCustomizerFeed("S1020", dt.LocalFilePath.Replace(".csv", "-Vision-Account.csv"));
                GenerateAdCustomizerFeed("S1040", dt.LocalFilePath.Replace(".csv", "-PortableTechnology-Account.csv"));
                GenerateAdCustomizerFeed("S1030", dt.LocalFilePath.Replace(".csv", "-Audio-Account.csv"));
                GenerateAdCustomizerFeed("S1060", dt.LocalFilePath.Replace(".csv", "-Accessories.csv"));
                GenerateAdCustomizerFeed("S1010", dt.LocalFilePath.Replace(".csv", "-SDA.csv"));
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
            // create the join
            using (var db = new ToolsContext())
            {
                var input = (from i in db.BccSourceFeedItems
                             join s in db.BccStoreStockItems on i.ClientProductId equals s.ClientProductId
                             join sd in db.StoreDatas on s.GmbStoreId equals sd.GmbStoreId
                             where sd.ClientName == "BCC" && i.AccountId == accountId && s.StockQuantity > 1
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
                                 s.StockQuantity,
                                 LocalPrice = s.Price,
                                 sd.StoreName,
                                 sd.GoogleLocationCanonicalName
                             }).ToList();

                

                foreach (var r in input)
                {
                    var lac = new LocalAdCustomizer();
                    // set header once
                    if (header.Count == 0) header = lac.Header;
                    // construct campaignName
                    lac.CampaignName = "NB_Producten_" + r.CategoryLevel2.Trim();
                    // construct adgroupName
                    lac.AdgroupName = r.ProductName.Trim();
                    // construct targetLocation
                    lac.TargetLocation = r.GoogleLocationCanonicalName.Trim();
                    lac.CustomId = lac.CampaignName + "!" + lac.AdgroupName + "!" + lac.TargetLocation;
                    lac.TargetLocationRestriction = "";
                    lac.SetScheduling(BusinessDataItem.DefaultSchedules.MonSat9To5);
                    lac.Price = r.Price;
                    lac.LocalPrice = r.LocalPrice;
                    lac.StoreName = r.StoreName;
                    lac.StockQuantity = r.StockQuantity.ToString();
                    lac.Brand = lac.PrettifyString(r.Brand);
                    lac.ProductName = r.ProductName;
                    lac.Type = r.Type;
                    lac.CategoryLevel1 = r.CategoryLevel1;
                    lac.CategoryLevel2 = r.CategoryLevel2;
                    lac.CategoryLevel3 = r.CategoryLevel3;

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

        private void OverwriteBccStoreStockData(string sourceFeedLocation)
        {
            // delete all records
            using (var db = new ToolsContext())
            {
                db.Database.ExecuteSqlCommand("DELETE FROM [BccStoreStockItem]");

                //db.Configuration.AutoDetectChangesEnabled = false;
                //var feedItems = db.BccStoreStockItems;
                //db.BccStoreStockItems.RemoveRange(feedItems);
                //db.SaveChanges();
            }
            // read the source file and add each record to the DB
            using (var sr = new StreamReader(sourceFeedLocation))
            {
                var reader = new CsvReader(sr);

                reader.Configuration.RegisterClassMap<BccStoreStockItemCsvMap>();

                var records = reader.GetRecords<BccStoreStockItem>().ToList();

                foreach(IEnumerable<BccStoreStockItem> batch in Partition(records, 1000))
                {
                    InsertBccStoreStockItems(batch.ToList());
                }
                
            }
        }

        private void InsertBccStoreStockItems(List<BccStoreStockItem> records)
        {
            using (var db = new ToolsContext())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                db.Configuration.ValidateOnSaveEnabled = false;

                db.BccStoreStockItems.AddRange(records);
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

        private void OverwriteBccSourceFeedData(string sourceFeedLocation)
        {
            // delete all records
            using (var db = new ToolsContext())
            {
                db.Database.ExecuteSqlCommand("DELETE FROM [BccSourceFeedItem]");


                //db.Configuration.AutoDetectChangesEnabled = false;
                //var feedItems = db.BccSourceFeedItems;
                //db.BccSourceFeedItems.RemoveRange(feedItems);
                //db.SaveChanges();
            }
            // read the source file and add each record to the DB
            using (var sr = new StreamReader(sourceFeedLocation))
            {
                var reader = new CsvReader(sr);

                reader.Configuration.RegisterClassMap<BccSourceFeedItemCsvMap>();

                var records = reader.GetRecords<BccSourceFeedItem>().ToList();

                foreach (IEnumerable<BccSourceFeedItem> batch in Partition(records, 1000))
                {
                    InsertBccSourceFeedItems(batch.ToList());
                }
            }
        }

        private void InsertBccSourceFeedItems(List<BccSourceFeedItem> records)
        {
            using (var db = new ToolsContext())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                db.Configuration.ValidateOnSaveEnabled = false;

                db.BccSourceFeedItems.AddRange(records);
                db.SaveChanges();
            }
        }

        private void OverwriteStoreData(string sourceFileLocation, string clientName)
        {
            // delete all records for this ClientName from DB
            using (var db = new ToolsContext())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                var stores = db.StoreDatas.Where(x => x.ClientName == clientName);
                db.StoreDatas.RemoveRange(stores);
                db.SaveChanges();
            }
            // read the source file and add each record to the DB
            using (var sr = new StreamReader(sourceFileLocation))
            {
                var reader = new CsvReader(sr);

                reader.Configuration.RegisterClassMap<StoreDataCsvMap>();
                reader.Configuration.Delimiter = ";";

                List<StoreData> records = reader.GetRecords<StoreData>().ToList();

                foreach (IEnumerable<StoreData> batch in Partition(records, 1000))
                {
                    InsertStoreData(batch.ToList());
                }
            }
        }

        private void InsertStoreData(List<StoreData> records)
        {
            using (var db = new ToolsContext())
            {
                db.Configuration.AutoDetectChangesEnabled = false;
                db.Configuration.ValidateOnSaveEnabled = false;

                db.StoreDatas.AddRange(records);
                db.SaveChanges();
            }
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
