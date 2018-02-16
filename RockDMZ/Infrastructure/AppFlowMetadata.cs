using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;

namespace RockDMZ.Infrastructure
{
    public class AppFlowMetadata
    {
        public AppFlowMetadata()
        {

        }

        private static readonly IAuthorizationCodeFlow flow =
            new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = "PUT_CLIENT_ID_HERE",
                    ClientSecret = "PUT_CLIENT_SECRET_HERE"
                },
                //Scopes = new[] { DriveService.Scope.Drive },
                //DataStore = new FileDataStore("Drive.Api.Auth.Store")
            });

        public string GetUserId(Microsoft.AspNetCore.Mvc.Controller controller)
        {
            // In this sample we use the session to store the user identifiers.
            // That's not the best practice, because you should have a logic to identify
            // a user. You might want to use "OpenID Connect".
            // You can read more about the protocol in the following link:
            // https://developers.google.com/accounts/docs/OAuth2Login.
            var user = "";
            return user.ToString();

        }

        public IAuthorizationCodeFlow Flow
        {
            get { return flow; }
        }
    }
}
