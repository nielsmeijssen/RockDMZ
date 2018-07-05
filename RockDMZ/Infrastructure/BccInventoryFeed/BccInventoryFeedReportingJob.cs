using CsvHelper;
using FluentFTP;
using Hangfire;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;

namespace RockDMZ.Infrastructure.BccInventoryFeed
{
    public class BccInventoryFeedReportingJob
    {
        [AutomaticRetry(Attempts = 0)]
        public void RecurringDownload(int id, int retry, string localFeedLocation)
        {
            try
            {
                retry++;
                var dt = GetDatatable(id);
                // see if there has already been a download
                var dateFirst = dt.LastDateDownloaded ?? dt.FirstDate;
                var dateLast = dt.LastDate ?? DateTime.MaxValue;
                if (dateFirst > DateTime.Today || dateFirst > dateLast) return;

                // get data
                // connect to the FTP server
                FtpClient client = new FtpClient();
                client.Host = "ftpservice.bcc.nl";
                client.Credentials = new NetworkCredential("ext.channable", "Starter123");
                client.DataConnectionType = FtpDataConnectionType.PASV;
                client.ReadTimeout = 120000;
                client.Connect();
                // download file
                var stream = new MemoryStream();
                if (client.Download(stream, "/PROD/Vergelijk-StoreStockCopy.xml"))
                {
                    var data = System.Text.Encoding.Default.GetString(stream.GetBuffer());
                    // process report
                    List<string> headers = new List<string>();
                    var report = GetReportsTableFromBccInventoryFeed(data, out headers, false, localFeedLocation);
                    // store data
                    AddToFile(report, dt.LocalFilePath, headers, false);

                    // update status of apidatatable
                    UpdateDatatable(id, DateTime.Now);
                }
                else
                {
                    throw new Exception("Download failed");
                }
            }
            catch(Exception ex)
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

        static List<List<string>> GetReportsTableFromBccInventoryFeed(string data, out List<string> header, bool downloadSample, string localFeedLocation)
        {
            var rtn = new List<List<string>>(500000);
            var counter = 0;
            // get the latest bronfeed
            var priceDict = new Dictionary<string, string>(500000);
            var storeItemList = new List<string>(500000); // list of all combinations of itemid and store code to avoid duplicates

            using (TextReader fileReader = File.OpenText(localFeedLocation + "af82ace1-3ecd-4066-af79-e5e31976811f.csv"))
            {
                var csv = new CsvReader(fileReader);
                var headers = csv.Parser.Read();
                // find the index of the id and the price field
                int priceIndex = -1;
                int idIndex = -1;
                for (var i = 0; i < headers.Length; i++)
                {
                    if (headers[i] == "price") priceIndex = i;
                    if (headers[i] == "id") idIndex = i;
                }
                // enumerate through the file and make dictionary with key:id and value:price
                while (true)
                {
                    var row = csv.Parser.Read();
                    if (row == null) break;

                    var rowid = row[idIndex];
                    var rowprice = row[priceIndex];

                    if (!priceDict.ContainsKey(rowid)) priceDict.Add(rowid, rowprice);
                }
            }
            // add header
            List<string> headerList = new List<string>();
            headerList.Add("store code");
            headerList.Add("itemid");
            headerList.Add("quantity");
            headerList.Add("price");
            header = headerList;
            // read xml from data
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(data);
            // transform xml to local products inventory csv format
            XmlNodeList products = xmlDoc.GetElementsByTagName("product");
            foreach (XmlNode product in products) // cycle the products collection
            {
                var itemId = product.SelectSingleNode("Product_id").InnerText;

                // find the price for this item in "BCC Bron Feed (latest)"
                // make sure you use the same source as is used for the shopping feed to avoid "Offer does not exist." errors
                if (!priceDict.ContainsKey(itemId)) continue;
                var price = priceDict[itemId];

                XmlNodeList stores = product.SelectNodes("stockDetails");
                foreach (XmlNode store in stores)
                {
                    var storeCode = store.Attributes.GetNamedItem("store_nb").Value.Trim(new[] { '0' });
                    if (storeItemList.Contains(storeCode + "#" + itemId)) continue; // check for dups to avoid "Duplicate item in the same store" error
                    else storeItemList.Add(storeCode + "#" + itemId);

                    if (storeCode == "1" || storeCode == "7") continue; // old BCC storecodes add to get 0-error feed > todo: remove

                    var quantity = store.Attributes.GetNamedItem("stock").Value;
                    var r = new List<string>();
                    r.Add(storeCode);
                    r.Add(itemId);
                    r.Add(quantity);
                    r.Add(price);
                    rtn.Add(r);
                }

                counter++;
                if (downloadSample && counter == 5) break;
            }

            return rtn;
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
