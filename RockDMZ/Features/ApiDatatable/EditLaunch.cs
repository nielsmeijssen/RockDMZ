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

namespace RockDMZ.Features.ApiDatatable
{
    public class EditLaunch
    {
        public class Query : IRequest<Model>
        {
            public int? Id { get; set; }
            public string JsonSecret { get; set; }
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
                            var lastDate = rtn.FirstDate.AddDays(90).ToString("yyyy-MM-dd");
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
                    var apiDatatable = await _db.ApiDatatables.FindAsync(message.Id);

                    apiDatatable.IsActive = true;

                    // schedule initial download task
                    BackgroundJob.Enqueue<GoogleAnalyticsReportingJob>(x => x.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                    // BackgroundJob.Enqueue(() => GoogleAnalyticsReportingJob.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary));
                    //schedule recurring download
                    if ((apiDatatable.LastDate != null && apiDatatable.LastDate >= DateTime.Today) || apiDatatable.UpdateSchedule != UpdateSchedule.None)
                    { 
                        var cronString = "";
                        switch(apiDatatable.UpdateSchedule)
                        {
                            case UpdateSchedule.Daily4am:
                                cronString = "0 4 * * *";
                                break;
                            case UpdateSchedule.Daily6am:
                                cronString = "0 6 * * *";
                                break;
                            case UpdateSchedule.Daily8am:
                                cronString = "0 8 * * *";
                                break;
                            case UpdateSchedule.Hourly:
                                cronString = "0 * * * *";
                                break;
                            case UpdateSchedule.WeeklyMonday6am:
                                cronString = "0 6 * * MON";
                                break;
                            default:
                                break;
                        }
                        RecurringJob.AddOrUpdate<GoogleAnalyticsReportingJob>("datatable_" + apiDatatable.Id, x => x.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
                        //RecurringJob.AddOrUpdate("datatable_" + apiDatatable.Id, () => QueryHandler.RecurringDownload(apiDatatable.Id, message.JsonSecret, message.DatatablesTemporary), cronString, TimeZoneInfo.Local);
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
