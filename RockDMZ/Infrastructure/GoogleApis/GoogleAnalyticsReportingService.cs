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

namespace RockDMZ.Infrastructure.GoogleApis
{
    public class GoogleAnalyticsReportingService : IDisposable
    {
        AnalyticsReportingService svc;
        int errorCount = 0;

        public void  InitService(UserCredential credential)
        {
            try
            {
                svc = new AnalyticsReportingService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "RockDMZ" // "SearchTechnologies GA Data Sucker"
                    });
            }
            catch(Exception ex)
            {
                Thread.Sleep(1000);
                InitService(credential);
            }
            
        }

        public void GetGoogleReport(string viewIds, string apiQuery, DateTime startDate, DateTime endDate, string tempFileLocation, string localFilePath)
        {
            try
            {
                if (svc == null) throw new Exception("No service available");

                var headers = new List<string>();

                foreach (var viewId in viewIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string pageToken = "";
                    var allResults = new List<Report>();
                    while (pageToken != null)
                    {
                        // pageToken = null;
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
                        else
                        {
                            pageToken = null;
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

                MergeTempFileToFile(localFilePath, tempFileLocation, headers);
            }
            catch(Exception ex)
            {
                // error ocurred
                errorCount++;
                // remove tempfile
                File.Delete(tempFileLocation);
                // try again
                if (errorCount < 10)
                {
                    Thread.Sleep(1000);
                    GetGoogleReport(viewIds, apiQuery, startDate, endDate, tempFileLocation, localFilePath);
                }

            }
            finally
            {
                svc.Dispose();
            }
        }


        public void Dispose()
        {
            if (svc != null) svc.Dispose();
        }

        private List<Metric> GetMetrics(string csvMetricsDimensions)
        {
            var rtn = new List<Metric>();
            var mds = csvMetricsDimensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var md in mds)
            {
                if (md.StartsWith("m|"))
                {
                    var metric = md.Substring(2);
                    rtn.Add(new Metric() { Expression = metric });
                }
            }
            return rtn;
        }

        private List<Dimension> GetDimensions(string csvMetricsDimensions)
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

        private void AddToTempFile(ReportsTable data, string fileLocation)
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
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }

        }

        private void MergeTempFileToFile(string fileLocation, string tempFileLocation, List<string> headers)
        {
            try
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
            catch(Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }
        }
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

                    foreach (var di in row.Dimensions)
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

        public string StringToCSVCell(string str)
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
    }
}
