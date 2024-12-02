using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ADPClient;
using ChoETL;
using Newtonsoft.Json.Linq;
using ParametersParser;

namespace WorkersAPI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.InitConnection();
            ParameterParser parser = new ParameterParser("ADP Workers API Utility", "This command line tool pulls Workers data from ADP Workers API endpoint");
            parser.Parameter("associate", ParameterParser.AllowType.Optional, "ADP AssociateOID");

            try
            {
                bool isSingle = args.Any() && !string.IsNullOrEmpty(parser["associate"]);
                if (args.Length > 0)
                {
                    parser.Parse(args);
                }

                await connection.connectAsync();
                if (connection.isConnectedIndicator())
                {
                    int skip = 0;
                    string result;
                    HttpResponseMessage APIResponse;
                    JObject str = new JObject();
                    Console.WriteLine("Getting Workers API data");

                    if (isSingle)
                    {
                        APIResponse = await connection.getADPDataAsync("/hr/v2/workers/" + parser["associate"]);
                        result = await APIResponse.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(result))
                        {
                            var resultArray = JObject.Parse(result);
                            str.Merge(resultArray);
                        }
                    }
                    else
                    {
                        do
                        {
                            APIResponse = await connection.getADPDataAsync($"/hr/v2/workers?$top=100&$skip={skip}");
                            result = await APIResponse.Content.ReadAsStringAsync();

                            if (!string.IsNullOrEmpty(result))
                            {
                                var resultArray = JObject.Parse(result);
                                str.Merge(resultArray);
                                skip += 100;
                            }
                        } while (APIResponse.StatusCode != HttpStatusCode.NoContent);
                    }

                    string JsonResult = str.ToString(Newtonsoft.Json.Formatting.None);

                    Console.WriteLine("Write parsed data to CSV");
                    using (StreamWriter writer = new StreamWriter("WorkersAPI.csv", false, Encoding.UTF8))
                    {
                        await writer.WriteAsync(WorkersAPI.ParseJson(JsonResult).ToString());
                    }

                    return 0;
                }
                else throw new Exception("Connection to ADP API failed");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                int errorCode = ex.HResult;
                return (errorCode == 0 ? -1 : errorCode);
            }
        }

    }
}

