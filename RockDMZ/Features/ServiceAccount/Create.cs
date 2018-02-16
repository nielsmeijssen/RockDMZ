namespace RockDMZ.Features.ServiceAccount
{
    using System.ComponentModel.DataAnnotations;
    using AutoMapper;
    using MediatR;
    using RockDMZ.Domain;
    using Infrastructure;
    using FluentValidation;
    using System;
    using System.Threading;
    using Google.Apis.AnalyticsReporting.v4;
    using Google.Apis.AnalyticsReporting.v4.Data;
    using Google.Apis.Auth.OAuth2;
    using Google.Apis.Services;
    using Google.Apis.Util.Store;
    using System.IO;
    using System.Threading.Tasks;
    using System.ComponentModel.DataAnnotations.Schema;
    using Google.Apis.Analytics.v3;

    public class Create
    {
        public class Command : IRequest
        {
            public ServiceName ServiceName { get; set; }
            public CredentialType CredentialType { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string JsonSecret { get; set; }
            public string KeyLocation { get; set; }
            public string FriendlyName { get; set; }
            public string CredentialsDirectory { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.ServiceName).NotNull();
                RuleFor(m => m.CredentialType).NotNull();
                RuleFor(m => m.Email).NotNull().Length(5, 255);
                RuleFor(m => m.Password).NotNull().When(m => m.CredentialType.Equals(CredentialType.Service));                
            }
        }

        public class Handler : IRequestHandler<Command>
        {
            private readonly ToolsContext _db;

            public Handler(ToolsContext db)
            {
                _db = db;
            }

            public void Handle(Command message)
            {
                var serviceAccount = Mapper.Map<Command, ServiceAccount>(message);
                var guid = Guid.NewGuid();
                var keyLocation = message.CredentialsDirectory + guid + "\\" + message.Email.Replace("@", "_").Replace(".", "_");
                var secretJson = message.JsonSecret;
                serviceAccount.KeyLocation = keyLocation;
                serviceAccount.FriendlyName = message.ServiceName + " | " + message.Email;


                try
                {
                    switch (message.CredentialType)
                    {
                        case CredentialType.WebUser:
                            var credential = GetCredential(serviceAccount.Email, secretJson, keyLocation).Result;
                            using (var svc = new AnalyticsReportingService(
                                new BaseClientService.Initializer
                                {
                                    HttpClientInitializer = credential,
                                    ApplicationName = "RockDMZ" // "SearchTechnologies GA Data Sucker"
                            }))
                                break;
                        default:
                            break;
                    }
                    _db.ServiceAccounts.Add(serviceAccount); // only store if key has been stored successfully
                }
                catch(Exception ex)
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }
                
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
    }
}
