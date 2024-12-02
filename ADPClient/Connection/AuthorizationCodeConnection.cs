using ADPClient.ADPException;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;


namespace ADPClient
{

    /// <summary>
    /// 
    /// </summary>
    public class AuthorizationCodeConnection : ADPApiConnection
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_connectionConfiguration"></param>
        public AuthorizationCodeConnection(ConnectionConfiguration _connectionConfiguration)
            : base((AuthorizationCodeConfiguration)_connectionConfiguration)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ADPProductURL"></param>
        /// <returns></returns>
        /// 
        public override async Task<HttpResponseMessage> getADPDataAsync(string ADPProductURL)
        {
            ADPAccessToken token = await getAccessTokenAsync(); // Assuming getAccessToken has been correctly updated to an async version

            if (isConnectedIndicator() && (token != null))
            {
                // Data dictionary preparation seems to be intended for a POST request, as GET requests do not typically include a body.
                // If you need to add query parameters to a GET request, they should be appended to the URL.

                // Adjusted to use HttpClient for an asynchronous GET request. 
                // Note: This example shows a GET request without sending data as body because it's not typically supported/expected.
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

                    // If you need to send data as query parameters, you should append them to the URL here.
                    var response = await client.GetAsync(ADPProductURL); // Ensure ADPProductURL contains any necessary query parameters

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new ADPConnectionException($"Connection Exception: {response.StatusCode}: {errorContent}");
                    }
                    return response;
                }
            }
            else
            {
                throw new ADPConnectionException("Connection Exception: connection not established.");
            }
        }

        //public override async Task<HttpResponseMessage> getADPDataAsync(string ADPProductURL)
        //{
        //    //string serverResponse = null;
        //    ADPAccessToken token = getAccessToken();
        //    Dictionary<string, string> data = null;

        //    if (isConnectedIndicator() && (token != null))
        //    {
        //        data = new Dictionary<string, string>();

        //        data.Add("client_id", ((AuthorizationCodeConfiguration)connectionConfiguration).clientID);
        //        data.Add("client_secret", ((AuthorizationCodeConfiguration)connectionConfiguration).clientSecret);
        //        data.Add("grant_type", ((AuthorizationCodeConfiguration)connectionConfiguration).grantType);
        //        data.Add("code", ((AuthorizationCodeConfiguration)connectionConfiguration).authorizationCode);
        //        data.Add("redirect_uri", ((AuthorizationCodeConfiguration)connectionConfiguration).redirectURL);


        //        // send the data to ADP server/s
        //        // since we have a valid token
        //        return SendWebRequest(ADPProductURL, data, new AuthenticationHeaderValue(token.TokenType, token.AccessToken), "application/json", "GET");
        //    }
        //    else
        //    {
        //        throw new ADPConnectionException("Connection Exception: connection not established.");
        //    }
        //    //return serverResponse;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override async Task<ADPAccessToken> getAccessTokenAsync()
        {
            if (!isConnectedIndicator())
            {
                AuthorizationCodeConfiguration conconfig = (AuthorizationCodeConfiguration)connectionConfiguration;
                Dictionary<string, string> data = new Dictionary<string, string>
        {
            {"client_id", conconfig.clientID},
            {"client_secret", conconfig.clientSecret},
            {"grant_type", conconfig.grantType},
            {"code", conconfig.authorizationCode},
            {"redirect_uri", conconfig.redirectURL}
        };

                var response = await SendWebRequestAsync(conconfig.tokenServerURL, data, null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    if (!String.IsNullOrEmpty(result))
                    {
                        var token = JSONUtil.Deserialize<ADPAccessToken>(result);
                        Status = "connected";
                        return token;
                    }
                }
            }
            return accessToken; // Return existing token if not updated
        }


        /// <summary>
        ///
        /// </summary>
        public void refreshToken()
        {
            // not yet
        }

        /// <summary>
        /// 
        /// </summary>
        public string RefreshToken { get; set; }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>String</returns>
        public string getAuthorizationURL()
        {
            AuthorizationCodeConfiguration config = (AuthorizationCodeConfiguration)connectionConfiguration;

            string authorizationurl = null;

            if (config != null)
            {

                if (String.IsNullOrEmpty(config.baseAuthorizationURL))
                {
                    throw new Exception("Missing authorization url.");
                }

                if (String.IsNullOrEmpty(config.clientID) || String.IsNullOrEmpty(config.clientSecret))
                {
                    throw new Exception("Missing client information.");
                }

                if (String.IsNullOrEmpty(config.redirectURL))
                {
                    throw new Exception("Missing callback/redirect url information");
                }

                state = Guid.NewGuid().ToString();

                authorizationurl = String.Format("{0}?client_id={1}&response_type={2}&redirect_uri={3}&scope=openid&state={4}",
                    config.baseAuthorizationURL,
                    HttpUtility.UrlEncode(config.clientID),
                    HttpUtility.UrlEncode(config.responseType),
                    HttpUtility.UrlEncode(config.redirectURL),
                    HttpUtility.UrlEncode(state));
            }
            else
            {
                throw new ADPConnectionException("Authorization code configuration not available");
            }

            return authorizationurl;
        }

        /// <summary>
        /// 
        /// </summary>
        protected string Status { get; set; }
    }
}

