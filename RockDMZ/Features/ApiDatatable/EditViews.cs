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

namespace RockDMZ.Features.ApiDatatable
{
    public class EditViews
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
            public List<ServiceView> ServiceViews { get; set; }

            public class ServiceView
            {
                public string Id { get; set; }
                public string ViewName { get; set; }
                public bool Selected { get; set; }
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

                switch (sa.ServiceName)
                {
                    case ServiceName.GoogleAnalytics:
                        result.ServiceViews = GetGoogleAnalyticsServiceViews(sa.Email, jsonSecret, sa.KeyLocation, dt.CsvViewIds);
                        break;
                    default:
                        break;
                }

                return result;
            }

            private List<Result.ServiceView> GetGoogleAnalyticsServiceViews(string email, string secretJson, string keyLocation, string csvViewIds)
            {
                var serviceViews = new List<Result.ServiceView>();
                var credential = GetCredential(email, secretJson, keyLocation).Result;
                var viewIds = csvViewIds?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                var svc = new AnalyticsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "RockDMZ"
                });
                var list = svc.Management.AccountSummaries.List();
                var feed = list.Execute();

                while (feed.Items != null)
                {
                    foreach (AccountSummary account in feed.Items)
                    {
                        var accountName = account.Name + " | ";
                        foreach (WebPropertySummary wp in account.WebProperties)
                        {
                            var propertyName = accountName + wp.Name + " | ";
                            if (wp.Profiles != null)
                            {
                                foreach (ProfileSummary profile in wp.Profiles)
                                {
                                    var viewName = propertyName + profile.Name;
                                    var sv = new Result.ServiceView { Id = profile.Id, ViewName = viewName, Selected = (viewIds != null && viewIds.Contains(profile.Id)) };
                                    serviceViews.Add(sv);
                                }
                            }
                        }
                    }
                    if (feed.NextLink == null) break;

                    list.StartIndex = feed.StartIndex + 1000;

                    feed = list.Execute();
                }

                return serviceViews;

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
            public List<string> ViewId { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
                RuleFor(m => m.ViewId).NotEmpty();
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

                var viewsChanged = false;

                var currentViews = apiDatatable.CsvViewIds?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (currentViews != null && currentViews.Length == message.ViewId.Count)
                {
                    foreach (var v in currentViews)
                    {
                        if (!message.ViewId.Contains(v))
                        {
                            viewsChanged = true;
                            break;
                        }
                    }
                }
                else
                {
                    viewsChanged = true;
                    apiDatatable.LastDateDownloaded = null;
                }
                

                apiDatatable.CsvViewIds = String.Join(",", message.ViewId);

                _db.SaveChanges();

                if (viewsChanged && File.Exists(apiDatatable.LocalFilePath)) File.Delete(apiDatatable.LocalFilePath);
            }
        }
    }
}
