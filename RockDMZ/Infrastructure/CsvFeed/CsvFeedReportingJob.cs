using CsvHelper;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RockDMZ.Infrastructure.CsvFeed
{
    public class CsvReportingJob
    {
        public void RecurringDownload(int id, bool append, int retry, string localFeedLocation)
        {
            try
            {
                retry++;
                var dt = GetDatatable(id);
                // see if there has already been a download
                var dateFirst = append ? (dt.LastDateDownloaded ?? dt.FirstDate) : (dt.LastDateDownloaded ?? dt.FirstDate);
                var dateLast = dt.LastDate ?? DateTime.MaxValue;
                if (dateFirst > DateTime.Today || dateFirst > dateLast) return;

                // get data
                var bccLocation = dt.ServiceAccount.ServiceLocation;
                WebClient client = new WebClient();
                Stream stream = client.OpenRead(bccLocation);
                StreamReader reader = new StreamReader(stream);
                var data = reader.ReadToEnd();
                // process report
                List<string> headers = new List<string>();
                var report = GetReportsTableFromCsv(data, dt.ApiQuery, out headers, false);
                // store data
                AddToFile(report, dt.LocalFilePath, headers, append);

                // update status of apidatatable
                UpdateDatatable(id, DateTime.Now);
            }
            catch(Exception ex)
            {
                if (retry < 6)
                {
                    // sleep for 5 minutes
                    System.Threading.Thread.Sleep(300000);
                    RecurringDownload(id, append, retry, localFeedLocation);
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

        static List<List<string>> GetReportsTableFromCsv(string data, string apiQuery, out List<string> header, bool downloadSample = false)
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
                for (var i = 0; i < headers.Length; i++)
                {
                    if (columns.Contains(headers[i]))
                    {
                        columnIndexes.Add(i);
                        headerList.Add(headers[i]);
                    }
                }
                header = headerList;
                // add data
                while (true)
                {
                    var row = csv.Parser.Read();
                    if (row == null) break;

                    var r = new List<string>();
                    r.Add(DateTime.Now.ToString("yyyyMMdd")); // add date
                    r.Add(DateTime.Now.ToString("HH:mm")); // add time in format 23:59
                    foreach (var i in columnIndexes)
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
                            foreach(var header in headers)
                            {
                                csv.WriteField(header);
                            }
                            csv.NextRecord();
                            foreach (var row in data)
                            {
                                foreach(var record in row)
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
