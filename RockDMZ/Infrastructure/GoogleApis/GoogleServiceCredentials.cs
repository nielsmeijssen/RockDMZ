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
    public class GoogleServiceCredentials
    {
        public async Task<UserCredential> GetCredential(string email, string secretJson, string keyLocation, GoogleServiceType serviceType)
        {
            string[] scopes;
            switch(serviceType)
            {
                case GoogleServiceType.AnalyticsReporting:
                    scopes = new[] { AnalyticsReportingService.Scope.AnalyticsReadonly, AnalyticsService.Scope.AnalyticsReadonly };
                    break;
                case GoogleServiceType.AnalyticsManagement:
                    scopes = new[] { AnalyticsService.Scope.AnalyticsReadonly };
                    break;
                default:
                    throw new Exception("Unknown GoogleServiceType");
            }
            using (var stream = GenerateStreamFromString(secretJson))
            {
                return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    scopes,
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

        public enum GoogleServiceType { AnalyticsManagement, AnalyticsReporting}
    }
}

