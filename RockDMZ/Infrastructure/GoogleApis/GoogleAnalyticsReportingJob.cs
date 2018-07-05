using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RockDMZ.Domain;

namespace RockDMZ.Infrastructure.GoogleApis
{
    public class GoogleAnalyticsReportingJob
    {
        public void RecurringDownload(int id, string jsonSecret, string tempDirectory)
        {
            var dt = GetDatatable(id);

            var gsc = new GoogleServiceCredentials();

            var credential = gsc.GetCredential(dt.ServiceAccount.Email, jsonSecret, dt.ServiceAccount.KeyLocation, GoogleServiceCredentials.GoogleServiceType.AnalyticsReporting).Result;

            var tempFileLocation = tempDirectory + dt.Id + "_" + dt.Name + ".csv";

            var dateFirst = (dt.LastDateDownloaded?.AddDays(1) ?? dt.FirstDate);
            var dateLast = dt.IncludeDateOfDownload ? DateTime.Today : DateTime.Today.AddDays(-1);
            var step = dt.ReloadBufferSizeInDays;
            if (dateLast > dt.LastDate) dateLast = (DateTime)dt.LastDate;
            if (dateFirst > DateTime.Today || dateFirst > dateLast) return;
            bool breakLoop = false;

            var dateRangeStartDate = dateFirst;
            var dateRangeEndDate = dateFirst.AddDays(step) < dateLast ? dateFirst.AddDays(step) : dateLast;

            while (!breakLoop)
            {
                var gars = new GoogleAnalyticsReportingService();
                gars.InitService(credential);

                // get the reports for this daterange for each viewid
                gars.GetGoogleReport(dt.CsvViewIds, dt.ApiQuery, dateRangeStartDate, dateRangeEndDate, tempFileLocation, dt.LocalFilePath);

                // update lastdatedownloaded
                UpdateDatatable(id, dateRangeEndDate);

                // set the new daterange
                dateRangeStartDate = dateRangeEndDate.AddDays(1);
                dateRangeEndDate = dateRangeStartDate.AddDays(step);

                if (dateRangeEndDate > dateLast) dateRangeEndDate = dateLast;
                if (dateRangeStartDate > DateTime.Today || dateRangeStartDate > dateLast) breakLoop = true;
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
    }
}
