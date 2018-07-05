using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using RockDMZ.Domain;
using System;
using System.Collections.Generic;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using System.IO;
using Google.Apis.Analytics.v3.Data;
using static RockDMZ.Features.ApiDatatable.EditMetrics.Result;
using System.ComponentModel.DataAnnotations;

namespace RockDMZ.Features.ApiDatatable
{
    public class EditMetrics
    {
        public class Query : IRequest<Result>
        {
            public int? Id { get; set; }
            public string JsonSecret { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class Result
        {
            public int Id { get; set; }
            public string CsvMetricsDimensions { get; set; }
            public List<ApiColumn> ApiColumns { get; set; }

            public class ApiColumn
            {
                public ApiColumn()
                {
                    Kind = "standard";
                }
                public string Id { get; set; }
                public string uiName { get; set; }
                public string Description { get; set; }
                public string Group { get; set; }
                public string Type { get; set; }
                public string Status { get; set; }
                public bool AllowedInSegments { get; set; }
                public string Calculation { get; set; }
                public bool Selected { get; set; }
                public string Kind { get ; set; }
            }

            public class CustomApiColumn : ApiColumn
            {
                public CustomApiColumn()
                {
                    Kind = "customdimension";
                }

                public bool? Active { get; set;}
                public DateTime? Created { get; set; }
                public DateTime? Updated { get; set; }
                public string Scope { get; set; }
            }
        }

        public class QueryHandler : IAsyncRequestHandler<Query, Result>
        {
            private readonly ToolsContext _db;

            public QueryHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Result> Handle(Query message)
            {
                var dt = _db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(c => c.Id == message.Id);
                var sa = dt.ServiceAccount;
                var jsonSecret = message.JsonSecret;

                var result = new Result();
                result.Id = dt.Id;
                result.CsvMetricsDimensions = dt.ApiQuery;

                switch (sa.ServiceName)
                {
                    case ServiceName.GoogleAnalytics:
                        result.ApiColumns = GetGoogleAnalyticsApiColumns(sa.Email, jsonSecret, sa.KeyLocation, dt.CsvViewIds, dt.ApiQuery);
                        break;
                    default:
                        break;
                }

                return result;
            }

            private List<Result.ApiColumn> GetGoogleAnalyticsApiColumns(string email, string secretJson, string keyLocation, string csvViewIds, string apiQuery)
            {
                var apiColumns = new List<Result.ApiColumn>();
                var credential = GetCredential(email, secretJson, keyLocation).Result;
                var viewIds = csvViewIds?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var metricIds = apiQuery?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                var svc = new AnalyticsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "RockDMZ"
                });

                var list = svc.Metadata.Columns.List("ga");
                var feed = list.Execute();

                foreach (var c in feed.Items)
                {
                    if (c.Attributes["uiName"].IndexOf("XX", StringComparison.CurrentCulture) != -1) // add api columns for GOALS 1-20
                    {
                        for(var i=1;i<21;i++)
                        {
                            var a = new ApiColumn();
                            a.AllowedInSegments = c.Attributes.ContainsKey("allowedInSegments") ? Convert.ToBoolean(c.Attributes["allowedInSegments"]) : false;
                            a.uiName = c.Attributes["uiName"].Replace("XX",i.ToString());
                            a.Calculation = c.Attributes.ContainsKey("calculation") ? c.Attributes["calculation"] : "";
                            a.Description = c.Attributes["description"];
                            a.Group = c.Attributes["group"];
                            a.Id = c.Id.Replace("XX", i.ToString());
                            a.Status = c.Attributes["status"];
                            a.Type = c.Attributes["type"];
                            a.Selected = (metricIds != null && metricIds.Contains(a.Type == "METRIC" ? "m|" + a.Id : "d|" + a.Id));
                            apiColumns.Add(a);
                        }
                    }
                    else
                    {
                        var a = new ApiColumn();
                        a.AllowedInSegments = c.Attributes.ContainsKey("allowedInSegments") ? Convert.ToBoolean(c.Attributes["allowedInSegments"]) : false;
                        a.uiName = c.Attributes["uiName"];
                        a.Calculation = c.Attributes.ContainsKey("calculation") ? c.Attributes["calculation"] : "";
                        a.Description = c.Attributes["description"];
                        a.Group = c.Attributes["group"];
                        a.Id = c.Id;
                        a.Status = c.Attributes["status"];
                        a.Type = c.Attributes["type"];
                        a.Selected = (metricIds != null && metricIds.Contains(a.Type == "METRIC" ? "m|" + a.Id : "d|" + a.Id));
                        apiColumns.Add(a);
                    }
                }

                // get the accountid and propertyid of the first view in viewIds
                var accounts = svc.Management.AccountSummaries.List();
                var accountsFeed = accounts.Execute();
                var accountId = "";
                var propertyId = "";
                var breakFlag = false;

                while (accountsFeed.Items != null)
                {
                    foreach (AccountSummary account in accountsFeed.Items)
                    {
                        accountId = account.Id;
                        foreach (WebPropertySummary wp in account.WebProperties)
                        {
                            propertyId = wp.Id;
                            if (wp.Profiles != null)
                            {
                                foreach (ProfileSummary profile in wp.Profiles)
                                {
                                    if (profile.Id == viewIds[0]) { breakFlag = true; break; }
                                }
                            }
                            if (breakFlag) break;
                        }
                        if (breakFlag) break;
                    }
                    if (accountsFeed.NextLink == null || breakFlag) break;

                    accounts.StartIndex = accountsFeed.StartIndex + 1000;

                    accountsFeed = accounts.Execute();
                }

                // list customdimensions using management api
                var cdList = svc.Management.CustomDimensions.List(accountId, propertyId);
                var customDimensions = cdList.Execute();

                foreach (var c in customDimensions.Items)
                {
                    var a = new CustomApiColumn();
                    a.Id = c.Id;
                    a.uiName = c.Name;
                    a.Scope = c.Scope;
                    a.Created = c.Created;
                    a.Calculation = "";
                    a.Group = "";
                    a.Description = "";
                    a.AllowedInSegments = true;
                    a.Updated = c.Updated;
                    a.Active = c.Active;
                    a.Status = a.Active == true ? "ACTIVE" : "INACTIVE"; 
                    a.Type = "CUSTOMDIMENSION";
                    a.Selected = (metricIds != null && metricIds.Contains("cd|" + a.Id));
                    apiColumns.Add(a);
                }

                return apiColumns;

            }

            static async Task<UserCredential> GetCredential(string email, string secretJson, string keyLocation)
            {
                using (var stream = GenerateStreamFromString(secretJson))
                {
                    return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { AnalyticsService.Scope.Analytics },
                        email,
                        CancellationToken.None,
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
            public List<string> ColumnId { get; set; }
            [Display(Name = "Metrics and dimensions separated by comma's")]
            public string CsvMetricsDimensions { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
                RuleFor(m => m.ColumnId).NotEmpty().Unless(m => !String.IsNullOrEmpty(m.CsvMetricsDimensions));
                RuleFor(m => m.ColumnId).Must(m => m.Exists(x => x.StartsWith("m|"))).Unless(m => m.CsvMetricsDimensions != null && m.CsvMetricsDimensions.Contains("m|"));
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
                var apiDatatable = await _db.ApiDatatables.FindAsync(message.Id);

                var newColumns = message.ColumnId.Count == 0 ?
                    (message.CsvMetricsDimensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).ToList() :
                    message.ColumnId;

                var columnsChanged = false;

                var currentColumns = apiDatatable.ApiQuery?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (currentColumns != null && currentColumns.Length == newColumns.Count)
                {
                    foreach (var v in currentColumns)
                    {
                        if (!newColumns.Contains(v))
                        {
                            columnsChanged = true;
                            break;
                        }
                    }
                }
                else
                {
                    columnsChanged = true;
                    apiDatatable.LastDateDownloaded = null;
                }


                apiDatatable.ApiQuery = String.Join(",", newColumns);

                _db.SaveChanges();

                if (columnsChanged && File.Exists(apiDatatable.LocalFilePath)) File.Delete(apiDatatable.LocalFilePath);
            }
        }
    }
}
