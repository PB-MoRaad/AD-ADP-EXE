using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ParametersParser;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UpdateAD
{
    class Program
    {
        private static System.Collections.Specialized.StringDictionary Parameters = new System.Collections.Specialized.StringDictionary();

        static int Main(string[] args)
        {
            ParameterParser parser = new ParameterParser("Update AD User Profile", "This command line tool update Active Directory User Profile");
            parser.Parameter("samaccountname", ParameterParser.AllowType.Required, "AD SamAccountName");
            parser.Parameter("json", ParameterParser.AllowType.Required, "JSON containing AD Atribute");

            try
            {
           
                parser.Parse(args);

                var JsonObject = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(Parameters["json"]).FirstOrDefault();
                string sAccountName = Parameters["samaccountname"];

                using (DirectoryEntry dEntry = new DirectoryEntry("LDAP://HW3-DC02.prosperitybank.com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
                {
                    DirectorySearcher searcher = new DirectorySearcher(dEntry);
                    searcher.Filter = $"(&(objectClass=user)(samAccountName={sAccountName}))";
                    SearchResult sResult = searcher.FindOne();

                    if (sResult == null)
                    {
                        Console.Error.WriteLine($"Error: User with samaccount name {sAccountName} not found");
                        return -1;
                    }

                    DirectoryEntry userEntry = sResult.GetDirectoryEntry();

                    foreach (var attribute in JsonObject)
                    {
                        if (!string.IsNullOrEmpty(attribute.Value))
                        {
                            userEntry.Properties[attribute.Key].Value = attribute.Value;
                        }
                        else
                        {
                            userEntry.Properties[attribute.Key].Clear();
                        }

                        userEntry.CommitChanges();
                    }
                    return 0;
                }
            }
            catch (JsonReaderException)
            {
                Console.Error.WriteLine("Error: Invalid JSON format");
                return -1;

            }
            catch (DirectoryServicesCOMException dex)
            {
                Console.Error.WriteLine(dex.ToString());
                return -1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return -1;
            }

        }

        //private static void Parse(string Arguments)
        //{
        //    if (string.IsNullOrEmpty(Arguments)) 
        //    {
        //        throw new Exception("Error: No arguments were passed");
        //    }

        //    var cmdLineRegex = new Regex(@"(?:\-)(?<name>\w*)[:= ]'(?<value>\[\{.+}]|[^\']*?)'",
        //    //var cmdLineRegex = new Regex(@"(?:\-)(?<name>\w*)[:= ]'(?<value>[^\']*?)'",
        //        RegexOptions.IgnoreCase | RegexOptions.Multiline);
        //    Match cmdLineMatch = cmdLineRegex.Match(Arguments.ToString());

        //    if (cmdLineMatch.Success)
        //    {
        //        while (cmdLineMatch.Success)
        //        {
        //            Parameters.Add(cmdLineMatch.Groups["name"].Value,
        //                    cmdLineMatch.Groups["value"].Value);
        //            cmdLineMatch = cmdLineMatch.NextMatch();
        //        }
        //    }
        //}
    }
}
