using FluentValidation;
using MediatR;
using RockDMZ.Domain;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Google.Apis.Services;
using Google.Apis.AnalyticsReporting.v4;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Analytics.v3;
using System.Threading;
using Google.Apis.Util.Store;
using System.IO;
using Google.Apis.AnalyticsReporting.v4.Data;
using Hangfire;
using System.Text;
using RockDMZ.Infrastructure.GoogleApis;
using System.Net;
using CsvHelper;
using FluentFTP;
using System.Xml;
using RockDMZ.Infrastructure.CsvFeed;
using RockDMZ.Infrastructure.BccInventoryFeed;
using RockDMZ.Infrastructure.BccLocalProductFeed;
using RockDMZ.Infrastructure.BccStoreStockTextAds;
using RockDMZ.Infrastructure.BccCategoryLevelBusinessData;
using RockDMZ.Infrastructure.BccProductLevelBusinessData;

namespace RockDMZ.Features.ApiDatatable
{
    public class EditLaunch
    {
        public class Query : IRequest<Model>
        {
            public int? Id { get; set; }
            public string JsonSecret { get; set; }
            public string DatatablesDirectory { get; set; }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class Model
        {
            public int Id { get; set; }
            public string LocalFilePath { get; set; }
            public string Url { get; set; }
            public int ServiceAccountId { get; set; }
            public string Name { get; set; }
            public DateTime FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public bool IncludeDateOfDownload { get; set; }
            public int ReloadBufferSizeInDays { get; set; }
            public UpdateSchedule UpdateSchedule { get; set; }
            public int LookbackWindowInDays { get; set; }
            public Domain.ServiceAccount ServiceAccount { get; set; }
            public string CsvViewIds { get; set; }
            public string ApiQuery { get; set; }
            public string ApiError { get; set; }
            public List<List<string>> ApiResults { get; set; }

        }

        public class QueryHandler : IAsyncRequestHandler<Query, Model>
        {
            private readonly ToolsContext _db;

            public QueryHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Model> Handle(Query message)
            {
                var rtn = await _db.ApiDatatables.Include("ServiceAccount").Where(i => i.Id == message.Id).ProjectToSingleOrDefaultAsync<Model>();

                if (rtn.ServiceAccount.ServiceName == ServiceName.GoogleAnalytics)
                { 
                    switch (rtn.ServiceAccount.CredentialType)
                    {
                        case CredentialType.WebUser:
                            var credential = GetCredential(rtn.ServiceAccount.Email, message.JsonSecret, rtn.ServiceAccount.KeyLocation).Result;
                            using (var svc = new AnalyticsReportingService(
                                new BaseClientService.Initializer
                                {
                                    HttpClientInitializer = credential,
                                    ApplicationName = "RockDMZ" // "SearchTechnologies GA Data Sucker"
                                }))
                            {
                                var body = new GetReportsRequest();
                                var requestItem = new ReportRequest();
                                var firstDate = rtn.FirstDate.ToString("yyyy-MM-dd");
                                var lastDate = rtn.FirstDate.AddDays(1).ToString("yyyy-MM-dd");
                                DateRange dateRange = new DateRange() { StartDate = firstDate, EndDate = lastDate };
                                requestItem.DateRanges = new List<DateRange>() { dateRange };
                                requestItem.PageSize = 50;
                                requestItem.SamplingLevel = "LARGE";
                                requestItem.ViewId = "ga:" + rtn.CsvViewIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                requestItem.Metrics = GetMetrics(rtn.ApiQuery);
                                requestItem.Dimensions = GetDimensions(rtn.ApiQuery);
                                body.ReportRequests = new List<ReportRequest>() { requestItem };
                                var reports = svc.Reports.BatchGet(body).Execute();
                                rtn.ApiResults = GetReportsTable(reports.Reports, requestItem.ViewId);
                                break;
                            }
                            
                        default:
                            break;
                    }
                }
                if (rtn.ServiceAccount.ServiceName == ServiceName.CsvFeedAppend || rtn.ServiceAccount.ServiceName == ServiceName.CsvFeedOverwrite)
                {
                    // get feed
                    var bccLocation = rtn.ServiceAccount.ServiceLocation;
                    WebClient client = new WebClient();
                    Stream stream = client.OpenRead(bccLocation);
                    StreamReader reader = new StreamReader(stream);
                    var data = reader.ReadToEnd();
                    // transform feed into ApiResults object
                    rtn.ApiResults = GetReportsTableFromCsv(data, rtn.ApiQuery, true);
                }

                if (rtn.ServiceAccount.ServiceName == ServiceName.BCCLocalInventoryFeed)
                {
                    // connect to the FTP server
                    FtpClient client = new FtpClient();
                    client.Host = "ftpservice.bcc.nl";
                    client.Credentials = new NetworkCredential("ext.channable", "Starter123");
                    client.DataConnectionType = FtpDataConnectionType.PASV;
                    client.ReadTimeout = 30000;
                    client.Connect();
                    // download file
                    var stream = new MemoryStream();
                    if (client.Download(stream, "/PROD/Vergelijk-StoreStockCopy.xml"))
                    {
                        var data = System.Text.Encoding.Default.GetString(stream.GetBuffer());
                        // transform feed into ApiResults object
                        rtn.ApiResults = GetReportsTableFromBccInventoryFeed(data, true, message.DatatablesDirectory);
                    }
                    else
                    {
                        throw new Exception("Download failed");
                    }
                }

                if (rtn.ServiceAccount.ServiceName == ServiceName.BCCLocalProductFeed)
                {
                    // connect to the FTP server
                    FtpClient client = new FtpClient();
                    client.Host = "ftpservice.bcc.nl";
                    client.Credentials = new NetworkCredential("ext.channable", "Starter123");
                    client.DataConnectionType = FtpDataConnectionType.PASV;
                    client.ReadTimeout = 30000;
                    client.Connect();
                    // download file
                    var stream = new MemoryStream();
                    if (client.Download(stream, "/PROD/Vergelijk-StoreStockCopy.xml"))
                    {
                        var data = System.Text.Encoding.Default.GetString(stream.GetBuffer());
                        // transform feed into ApiResults object
                        rtn.ApiResults = GetReportsTableFromBccLocalProductsFeed(data, true, message.DatatablesDirectory);
                    }
                    else
                    {
                        throw new Exception("Download failed");
                    }
                }

                if (rtn.ServiceAccount.ServiceName == ServiceName.BCCStoreStockTextAds)
                {
                    rtn.ApiResults = new List<List<string>>();
                }

                if (rtn.ServiceAccount.ServiceName == ServiceName.BCCCategoryLevelBusinessData)
                {
                    rtn.ApiResults = GetReportTableFromBccCategoryLevelBusinessData("");
                }

                if (rtn.ServiceAccount.ServiceName == ServiceName.BCCProductLevelBusinessData)
                {
                    rtn.ApiResults = new List<List<string>>();
                }

                return rtn;
            }

            

            public static void GetGoogleApiReports(string email, string jsonSecret, string keyLocation, string viewIds, string apiQuery, DateTime startDate, DateTime endDate, string tempFileLocation, ref List<string> headers)
            {
                var credential = GetCredential(email, jsonSecret,keyLocation).Result;
                using (var svc = new AnalyticsReportingService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "RockDMZ" // "SearchTechnologies GA Data Sucker"
                    }))
                {
                    foreach (var viewId in viewIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string pageToken = "";
                        var allResults = new List<Report>();
                        while (pageToken != null)
                        {
                            pageToken = null;
                            var request = new ReportRequest();
                            var firstDate = startDate.ToString("yyyy-MM-dd");
                            var lastDate = endDate.ToString("yyyy-MM-dd");
                            DateRange dateRange = new DateRange() { StartDate = firstDate, EndDate = lastDate };
                            request.DateRanges = new List<DateRange>() { dateRange };
                            request.PageSize = 10000;
                            request.SamplingLevel = "LARGE";
                            request.ViewId = "ga:" + viewId;
                            request.Metrics = GetMetrics(apiQuery);
                            request.Dimensions = GetDimensions(apiQuery);
                            request.PageToken = pageToken == "" ? null : pageToken;  // send null for 1st request
                                                                                     // get the report
                            var body = new GetReportsRequest();
                            body.ReportRequests = new List<ReportRequest>() { request };

                            var reports = svc.Reports.BatchGet(body).Execute();
                            Thread.Sleep(1000);
                            if (reports?.Reports?[0]?.Data?.RowCount != null)
                            {
                                allResults.AddRange(reports.Reports);
                                pageToken = reports.Reports[0].NextPageToken;
                                reports = null;
                            }
                        }
                        if (allResults.Count > 0)
                        {
                            var apiResults = new ReportsTable(allResults, viewId);
                            headers = apiResults.Headers;
                            // add the report to the file
                            AddToTempFile(apiResults, tempFileLocation);
                            allResults = null;
                            apiResults = null;
                        }
                    }
                }
            }

            //public static void RecurringDownload(int id, string jsonSecret, string tempDirectory)
            //{
            //    // get apidatatable
            //    using (var db = new ToolsContext("Server=KOBUTO\\SQLEXPRESS;Database=RockDB;MultipleActiveResultSets=true;Integrated Security=SSPI;"))
            //    {
            //        var dt = db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(x => x.Id.Equals(id));
            //        var tempFileLocation = tempDirectory + dt.Id + "_" + dt.Name + ".csv";
            //        var headers = new List<string>();
            //        var dateFirst = (dt.LastDateDownloaded?.AddDays(1) ?? dt.FirstDate);
            //        var dateLast = dt.IncludeDateOfDownload ? DateTime.Today : DateTime.Today.AddDays(-1);
            //        var step = dt.ReloadBufferSizeInDays;
            //        if (dateLast > dt.LastDate) dateLast = (DateTime)dt.LastDate;
            //        if (dateFirst > DateTime.Today || dateFirst > dateLast) return;
            //        bool breakLoop = false;

            //        var dateRangeStartDate = dateFirst;
            //        var dateRangeEndDate = dateFirst.AddDays(step) < dateLast ? dateFirst.AddDays(step) : dateLast;

            //        while (!breakLoop)
            //        {
            //            // get the reports for this daterange for each viewid
            //            GetGoogleApiReports(dt.ServiceAccount.Email, jsonSecret, dt.ServiceAccount.KeyLocation, dt.CsvViewIds, dt.ApiQuery, dateRangeStartDate, dateRangeEndDate, tempFileLocation, ref headers);
            //            // After all pages for a daterange/view have been downloaded -> add tempfile to file
            //            MergeTempFileToFile(dt.LocalFilePath, tempFileLocation, headers);
            //            // update lastdatedownloaded
            //            dt.LastDateDownloaded = dateRangeEndDate;
            //            db.SaveChanges();

            //            // set the new daterange
            //            dateRangeStartDate = dateRangeEndDate.AddDays(1);
            //            dateRangeEndDate = dateRangeStartDate.AddDays(step);

            //            if (dateRangeEndDate > dateLast) dateRangeEndDate = dateLast;
            //            if (dateRangeStartDate > DateTime.Today || dateRangeStartDate > dateLast) breakLoop = true;
            //        }
            //    }   
            //}

            //public static void InitialDownload(int id, string jsonSecret, string tempDirectory)
            //{
            //    RecurringDownload(id, jsonSecret, tempDirectory);
            //}

             /* public static void InitialDownload(int id, string jsonSecret, string tempDirectory)
            {
                // get apidatatable
                using (var db = new ToolsContext("Server=KOBUTO\\SQLEXPRESS;Database=RockDB;MultipleActiveResultSets=true;Integrated Security=SSPI;"))
                {
                    var dt = db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(x => x.Id.Equals(id));
                    var tempFileLocation = tempDirectory + dt.Id + "_" + dt.Name + ".csv";
                    var headers = new List<string>();
                    // prepare and check the daterange
                    var dateFirst = (dt.LastDateDownloaded?.AddDays(1) ?? dt.FirstDate);
                    var dateLast = (dt.LastDateDownloaded?.AddDays(1) ?? dt.FirstDate).AddDays(dt.ReloadBufferSizeInDays);
                    if (dateLast > DateTime.Today) dateLast = dt.IncludeDateOfDownload ? DateTime.Today : DateTime.Today.AddDays(-1);
                    if (dateLast > dt.LastDate) dateLast = (DateTime)dt.LastDate;

                    if (dateFirst > DateTime.Today || dateFirst > dateLast) return;
                    if (dateFirst == DateTime.Today && !dt.IncludeDateOfDownload) return;

                    var credential = GetCredential(dt.ServiceAccount.Email, jsonSecret, dt.ServiceAccount.KeyLocation).Result;
                    using (var svc = new AnalyticsReportingService(
                        new BaseClientService.Initializer
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = "RockDMZ" // "SearchTechnologies GA Data Sucker"
                        }))
                    {
                        // prepare a request 
                        foreach(var viewId in dt.CsvViewIds.Split(new[] { ','}, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string pageToken = "";
                            var allResults = new List<Report>();
                            while(pageToken != null)
                            {
                                pageToken = null;
                                var request = new ReportRequest();
                                var firstDate = dateFirst.ToString("yyyy-MM-dd");
                                var lastDate = dateLast.ToString("yyyy-MM-dd");
                                DateRange dateRange = new DateRange() { StartDate = firstDate, EndDate = lastDate };
                                request.DateRanges = new List<DateRange>() { dateRange };
                                request.PageSize = 10000;
                                request.SamplingLevel = "LARGE";
                                request.ViewId = "ga:" + viewId;
                                request.Metrics = GetMetrics(dt.ApiQuery);
                                request.Dimensions = GetDimensions(dt.ApiQuery);
                                request.PageToken = pageToken == "" ? null : pageToken;  // send null for 1st request
                                // get the report
                                var body = new GetReportsRequest();
                                body.ReportRequests = new List<ReportRequest>() { request };
                                
                                var reports = svc.Reports.BatchGet(body).Execute();
                                System.Threading.Thread.Sleep(1000);
                                if (reports?.Reports?[0]?.Data?.RowCount != null)
                                {
                                    allResults.AddRange(reports.Reports);
                                    pageToken = reports.Reports[0].NextPageToken;
                                }                                
                            }
                            if (allResults.Count > 0)
                            { 
                                var apiResults = new ReportsTable(allResults, viewId);
                                headers = apiResults.Headers;
                                // add the report to the file
                                AddToTempFile(apiResults, tempFileLocation);
                                // start with the next view
                                apiResults = null;
                                allResults = null;
                            }
                        }
                        // After all pages for a daterange/view have been downloaded -> add tempfile to file
                        MergeTempFileToFile(dt.LocalFilePath, tempFileLocation, headers);
                        // update lastdatedownloaded
                        dt.LastDateDownloaded = dateLast;
                        db.SaveChanges();
                    }
                }
                InitialDownload(id, jsonSecret, tempDirectory);
            }
            */

            public static void AddToTempFile(ReportsTable data, string fileLocation)
            {
                try
                {
                    using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                    using (StreamWriter sw = new StreamWriter(aFile))
                    {
                        foreach (var row in data.Data)
                        {
                            sw.WriteLine(String.Join(",", row));
                        }
                        data = null;
                    }
                }
                catch(Exception ex)
                {
                    File.Delete(fileLocation);
                }
                
            }

            public static void MergeTempFileToFile(string fileLocation, string tempFileLocation, List<string> headers)
            {
                if (headers == null || headers.Count == 0)
                {
                    return;
                }
                int i = 0;
                if (!File.Exists(fileLocation))
                {
                    using (StreamWriter sw = File.CreateText(fileLocation))
                    {
                        
                        sw.WriteLine(String.Join(",", headers));
                        using (var inputStream = File.OpenText(tempFileLocation))
                        {
                            string s = "";
                            while ((s = inputStream.ReadLine()) != null)
                            {
                                i++;
                                sw.WriteLine(s);
                                if (i % 100 == 0) sw.Flush(); 
                            }
                        }
                    }
                }
                else
                {
                    using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                    using (StreamWriter sw = new StreamWriter(aFile))
                    {
                        using (var inputStream = File.OpenText(tempFileLocation))
                        {
                            string s = "";
                            while ((s = inputStream.ReadLine()) != null)
                            {
                                i++;
                                sw.WriteLine(s);
                                if (i % 100 == 0) sw.Flush();
                            }
                        }
                    }
                }

                File.Delete(tempFileLocation);
            }

            public class ReportsTable
            {
                List<string> _headers;
                List<List<string>> _data;
                public ReportsTable(List<Report> reports, string viewId)
                {
                    _headers = new List<string>();
                    _data = new List<List<string>>(500000);
                    
                    foreach (Report report in reports)
                    {
                        if (_headers == null || _headers.Count == 0)
                        {
                            ColumnHeader header = report.ColumnHeader;
                            _headers.Add("GA View");
                            _headers.AddRange((List<string>)header.Dimensions);
                            List<MetricHeaderEntry> metricHeaders = (List<MetricHeaderEntry>)header.MetricHeader.MetricHeaderEntries;
                            _headers.AddRange(metricHeaders.Select(x => x.Name));
                        }

                        // List<ReportRow> rows = (List<ReportRow>)report.Data.Rows;

                        foreach (ReportRow row in report.Data.Rows)
                        {
                            var r = new List<string>();
                            r.Add(viewId);

                            foreach(var di in row.Dimensions)
                            {
                                r.Add(StringToCSVCell(di));
                            }
                            // r.AddRange((List<string>)row.Dimensions);

                            var metrics = row.Metrics;

                            for (int j = 0; j < metrics.Count(); j++)
                            {
                                DateRangeValues values = metrics[j];
                                r.AddRange(values.Values);
                            }
                            _data.Add(r);
                        }

                    }
                }

                public List<string> Headers
                {
                    get
                    {
                        return _headers;
                    }
                }
                public List<List<string>> Data
                {
                    get
                    {
                        return _data;
                    }
                }
            }

            static List<List<string>> GetReportsTableFromBccInventoryFeed(string data, bool downloadSample, string datatablesDirectory)
            {
                var rtn = new List<List<string>>(500000);
                var counter = 0;
                // get the latest bronfeed
                var priceDict = new Dictionary<string, string>(500000);
                var file = datatablesDirectory + "af82ace1-3ecd-4066-af79-e5e31976811f.csv";

                using (TextReader fileReader = File.OpenText(file))
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
                        if (row == null)
                        {
                            break;
                        }
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
                rtn.Add(headerList);
                // read xml from data
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(data);
                // transform xml to local products inventory csv format
                XmlNodeList products = xmlDoc.GetElementsByTagName("product"); 
                foreach (XmlNode product in products) // cycle the products collection
                {
                    var itemId = product.SelectSingleNode("Product_id").InnerText;

                    // find the price for this item in "BCC Bron Feed (latest)"
                    if (!priceDict.ContainsKey(itemId)) continue;
                    var price = priceDict[itemId];

                    XmlNodeList stores = product.SelectNodes("stockDetails");
                    foreach(XmlNode store in stores)
                    {
                        var storeCode = store.Attributes.GetNamedItem("store_nb").Value.Trim(new[] { '0' });
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

            static List<List<string>> GetReportTableFromBccCategoryLevelBusinessData(string data, bool downloadSample = false)
            {
                var items = new List<List<string>>();
                var header = new List<string>();
                var hashtable = new HashSet<string>();

                var campaigns = new AdWordsCampaignStructure();
                campaigns.CampaignTemplates = new List<AdWordsCampaignTemplate>();

                var cGeneric = new AdWordsCampaignTemplate() { NameTemplate = "NB_Generiek_#CategoryLevel2Singular#" };
                cGeneric.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel3Singular#]" });
                cGeneric.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel3Plural#]" });
                campaigns.CampaignTemplates.Add(cGeneric);

                var cIntentH = new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-High_#CategoryLevel2Singular#" };
                cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "bestellen" });
                cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "kopen" });
                cIntentH.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "online" });
                campaigns.CampaignTemplates.Add(cIntentH);

                var cIntentM = new AdWordsCampaignTemplate() { NameTemplate = "NB_Intent-Medium_#CategoryLevel2Singular#" };
                cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "aanbieding" });
                cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "goede" });
                cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "nieuw" });
                cIntentM.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "vergelijk" });
                campaigns.CampaignTemplates.Add(cIntentM);

                var cBCC = new AdWordsCampaignTemplate() { NameTemplate = "NB_BCC_#CategoryLevel2Singular#" };
                cBCC.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel3Singular#]" });
                cBCC.AdgroupTemplates.Add(new AdWordsAdgroupTemplate { NameTemplate = "[#CategoryLevel3Plural#]" });
                campaigns.CampaignTemplates.Add(cBCC);


                // create the join
                using (var db = new ToolsContext())
                {
                    var input = (from i in db.BccSourceFeedItems
                                 where i.AccountId == "BCC"
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
                        var lac = new CategoryLevelBusinessDataItem();
                        // set header once
                        if (header.Count == 0) header = lac.Header;

                        foreach (var ct in campaigns.CampaignTemplates)
                        {
                            var pt = r.CategoryLevel2.Replace(" en ", "-").Replace(" ", "-").Trim();
                            lac.CampaignName = ct.NameTemplate.Replace("#CategoryLevel2Singular#", pt);
                            lac.SetCategoryLevel1(r.CategoryLevel1);
                            lac.SetCategoryLevel2(r.CategoryLevel2);
                            lac.SetCategoryLevel3(r.CategoryLevel3);

                            foreach (var ag in ct.AdgroupTemplates)
                            {
                                var adgroupName = ct.NameTemplate.Replace("", r.CategoryLevel3.ToLower());
                                adgroupName = adgroupName.Replace("", lac.CategoryLevel3(true, "LC"));

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

                                // todo: calculate fromPrice & numberofproducts for level2 and level3

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
                return items;
            }

            static List<List<string>> GetReportsTableFromBccLocalProductsFeed(string data, bool downloadSample, string datatablesDirectory)
            {
                var rtn = new List<List<string>>(500000);
                var counter = 0;
                // get the latest bronfeed
                var titleDict = new Dictionary<string, string>(500000);
                var file = datatablesDirectory + "af82ace1-3ecd-4066-af79-e5e31976811f.csv";

                using (TextReader fileReader = File.OpenText(file))
                {
                    var csv = new CsvReader(fileReader);
                    var headers = csv.Parser.Read();
                    // find the index of the id and the title field
                    int titleIndex = -1;
                    int idIndex = -1;
                    for (var i = 0; i < headers.Length; i++)
                    {
                        if (headers[i] == "title") titleIndex = i;
                        if (headers[i] == "id") idIndex = i;
                    }
                    // enumerate through the file and make dictionary with key:id and value:price
                    while (true)
                    {
                        var row = csv.Parser.Read();
                        if (row == null)
                        {
                            break;
                        }
                        var rowid = row[idIndex];
                        var rowtitle = row[titleIndex];

                        if (!titleDict.ContainsKey(rowid)) titleDict.Add(rowid, rowtitle);
                    }
                }
                // add header
                List<string> headerList = new List<string>();
                List<string> distinctItems = new List<string>(500000);

                headerList.Add("itemid");
                headerList.Add("title");
                rtn.Add(headerList);
                // read xml from data
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(data);
                // transform xml to local products inventory csv format
                XmlNodeList products = xmlDoc.GetElementsByTagName("product");
                foreach (XmlNode product in products) // cycle the products collection
                {
                    var itemId = product.SelectSingleNode("Product_id").InnerText;

                    // find the price for this item in "BCC Bron Feed (latest)"
                    if (!titleDict.ContainsKey(itemId)) continue;
                    var title = titleDict[itemId];

                    if (distinctItems.Contains(itemId)) continue;
                    else distinctItems.Add(itemId);

                    var r = new List<string>();
                    r.Add(itemId);
                    r.Add(title);
                    rtn.Add(r);

                    counter++;
                    if (downloadSample && counter == 5) break;
                }

                return rtn;
            }

            static List<List<string>> GetReportsTableFromCsv(string data, string apiQuery, bool downloadSample = false)
            {
                var rtn = new List<List<string>>(500000);
                var counter = 0;
                var columns = apiQuery.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                // init csv parser
                using (var csv = new CsvReader(new StringReader(data)))
                {
                    // insert header
                    List<string> headerList = new List<string>();
                    headerList.Add("Download date");
                    headerList.Add("Download time");
                    var headers = csv.Parser.Read();
                    // find the columnIndex for the metrics/dimensions that we want to save
                    List<int> columnIndexes = new List<int>();
                    for(var i=0;i<headers.Length;i++)
                    {
                        if (columns.Contains(headers[i]))
                        {
                            columnIndexes.Add(i);
                            headerList.Add(headers[i]);
                        }
                    }
                    rtn.Add(headerList);
                    // add data
                    while(true)
                    {
                        var row = csv.Parser.Read();
                        if (row == null) break;

                        var r = new List<string>();
                        r.Add(DateTime.Now.ToString("yyyyMMdd")); // add date
                        r.Add(DateTime.Now.ToString("HH:mm")); // add time in format 23:59
                        foreach(var i in columnIndexes)
                        {
                            r.Add(row[i]);
                        }
                        rtn.Add(r);

                        counter++;
                        if (downloadSample && counter == 50) break;
                    }
                }
                return rtn;
            }


            static List<List<string>> GetReportsTable(IList<Report> reports, string viewId)
            {
                var rtn = new List<List<string>>(500000);
                if (reports?[0]?.Data?.RowCount == null) return rtn;
                foreach (Report report in reports)
                {
                    ColumnHeader header = report.ColumnHeader;
                    List<string> headerList = new List<string>();
                    headerList.Add("GA View");
                    headerList.AddRange((List<string>)header.Dimensions);
                    List<MetricHeaderEntry> metricHeaders = (List<MetricHeaderEntry>)header.MetricHeader.MetricHeaderEntries;
                    headerList.AddRange(metricHeaders.Select(x => x.Name));

                    rtn.Add(headerList);

                    // List<ReportRow> rows = (List<ReportRow>)report.Data.Rows;

                    foreach (ReportRow row in report.Data.Rows)
                    {
                        var r = new List<string>();
                        r.Add(viewId);

                        r.AddRange((List<string>)row.Dimensions);

                        var metrics = row.Metrics;

                        for (int j = 0; j < metrics.Count(); j++)
                        {
                            DateRangeValues values = metrics[j];
                            r.AddRange(values.Values);
                        }
                        rtn.Add(r);
                    }

                }
                return rtn;
            }

            static List<Metric> GetMetrics(string csvMetricsDimensions)
            {
                var rtn = new List<Metric>();
                var mds = csvMetricsDimensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach(var md in mds)
                {
                    if (md.StartsWith("m|"))
                    {
                        var metric = md.Substring(2);
                        rtn.Add(new Metric() { Expression = metric });
                    }
                }
                return rtn;
            }

            static List<Dimension> GetDimensions(string csvMetricsDimensions)
            {
                var rtn = new List<Dimension>();
                var mds = csvMetricsDimensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var md in mds)
                {
                    if (md.StartsWith("d|"))
                    {
                        var dimension = md.Substring(2);
                        rtn.Add(new Dimension() { Name = dimension });
                    }
                }
                return rtn;
            }

            /// <summary>
            /// Turn a string into a CSV cell output
            /// </summary>
            /// <param name="str">String to output</param>
            /// <returns>The CSV cell formatted string</returns>
            public static string StringToCSVCell(string str)
            {
                bool mustQuote = (str.Contains(",") || str.Contains("\"") || str.Contains("\r") || str.Contains("\n"));
                if (mustQuote)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("\"");
                    foreach (char nextChar in str)
                    {
                        sb.Append(nextChar);
                        if (nextChar == '"')
                            sb.Append("\"");
                    }
                    sb.Append("\"");
                    return sb.ToString();
                }

                return str;
            }

            static async Task<UserCredential> GetCredential(string email, string secretJson, string keyLocation)
            {
                using (var stream = GenerateStreamFromString(secretJson))
                {
                    return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { AnalyticsReportingService.Scope.AnalyticsReadonly, AnalyticsService.Scope.AnalyticsReadonly },
                        email, CancellationToken.None,
                        new FileDataStore(keyLocation, true));
                }
            }

            public static Stream GenerateStreamFromString(string s)
            {
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(s);
                writer.Flush();
                stream.Position = 0;
                return stream;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string JsonSecret { get; set; }
            public string DatatablesTemporary { get; set; }
            public string DatatablesDirectory { get; set; }
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
                try
                {
                    var apiDatatable = _db.ApiDatatables.Include("ServiceAccount").Where(i => i.Id == message.Id).SingleOrDefault();

                    apiDatatable.IsActive = true;

                    var cronString = "";

                    Random random = new Random(DateTime.Now.Second);
                    int randomNumber = random.Next(0, 60);

                    switch (apiDatatable.UpdateSchedule)
                    {
                        case UpdateSchedule.Daily4am:
                            cronString = randomNumber + " 4 * * *";
                            break;
                        case UpdateSchedule.Daily6am:
                            cronString = randomNumber + " 6 * * *";
                            break;
                        case UpdateSchedule.Daily8am:
                            cronString = randomNumber + " 8 * * *";
                            break;
                        case UpdateSchedule.Daily9am:
                            cronString = randomNumber + " 9 * * *";
                            break;
                        case UpdateSchedule.Daily10am:
                            cronString = randomNumber + " 10 * * *";
                            break;
                        case UpdateSchedule.Daily4pm:
                            cronString = randomNumber + " 16 * * *";
                            break;
                        case UpdateSchedule.Daily5pm:
                            cronString = randomNumber + " 17 * * *";
                            break;
                        case UpdateSchedule.Daily6pm:
                            cronString = randomNumber + " 18 * * *";
                            break;
                        case UpdateSchedule.Daily7pm:
                            cronString = randomNumber + " 19 * * *";
                            break;
                        case UpdateSchedule.Hourly:
                            cronString = randomNumber + " * * * *";
                            break;
                        case UpdateSchedule.WeeklyMonday6am:
                            cronString = randomNumber + " 6 * * MON";
                            break;
                        default:
                            break;
                    }

                    switch (apiDatatable.ServiceAccount.ServiceName)
                    {
                        case ServiceName.GoogleAnalytics:
                            // schedule initial download task
                            BackgroundJob.Enqueue<GoogleAnalyticsReportingJob>(x => x.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<GoogleAnalyticsReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.CsvFeedAppend:
                            // schedule initial download task
                            BackgroundJob.Enqueue<CsvReportingJob>(x => x.RecurringDownload(apiDatatable.Id, true, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<CsvReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, true, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.CsvFeedOverwrite:
                            // schedule initial download task
                            BackgroundJob.Enqueue<CsvReportingJob>(x => x.RecurringDownload(apiDatatable.Id, false, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<CsvReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, false, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.BCCLocalInventoryFeed:
                            // schedule initial download task
                            BackgroundJob.Enqueue<BccInventoryFeedReportingJob>(x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<BccInventoryFeedReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.BCCLocalProductFeed:
                            // schedule initial download task
                            BackgroundJob.Enqueue<BccLocalProductFeedReportingJob>(x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<BccLocalProductFeedReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.BCCStoreStockTextAds:
                            // schedule initial download task
                            BackgroundJob.Enqueue<BccStoreStockTextAdsReportingJob>(x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<BccStoreStockTextAdsReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.BCCCategoryLevelBusinessData:
                            // schedule initial download task
                            BackgroundJob.Enqueue<BccCategoryLevelBusinessDataReportingJob>(x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<BccCategoryLevelBusinessDataReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        case ServiceName.BCCProductLevelBusinessData:
                            // schedule initial download task
                            BackgroundJob.Enqueue<BccProductLevelBusinessDataReportingJob>(x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory));
                            // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                            //schedule recurring download
                            if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                            {

                                RecurringJob.AddOrUpdate<BccProductLevelBusinessDataReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, 0, message.DatatablesDirectory), cronString, TimeZoneInfo.Local);
                                //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                            }
                            break;
                        default:
                            throw new Exception("Unknown service name");
                    }

                    _db.SaveChanges();
                    _db.Dispose();
                }
                catch(Exception ex)
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }
            }
        }
    }
}
