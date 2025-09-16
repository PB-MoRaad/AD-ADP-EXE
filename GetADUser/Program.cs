using Newtonsoft.Json;
using ParametersParser;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;

namespace GetADUser
{
    class Program
    {
        static int Main(string[] args)
        {
            ParameterParser parser = new ParameterParser("Create AD User Account Utility", "This command line tool pulls Active Directory User Attributes");
            parser.Parameter("samaccountname", ParameterParser.AllowType.Required, "AD Sam Account Name");
            parser.Parameter("json", ParameterParser.AllowType.Required, "JSON containing all AD Atributes");

            try
            {
                parser.Parse(args);

                var Properties = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(parser["json"]);
                string[] requiredProperties = Properties.Select(m => m.Select(x => x.Value).FirstOrDefault()).ToArray();

                // Add New Required Property
                List<string> propertyList = requiredProperties.ToList();
                propertyList.Add("HomeFolderExistsYN");
                requiredProperties = propertyList.ToArray();

                using (DirectoryEntry dEntry = new DirectoryEntry("LDAP://HW3-DC02.prosperitybank.com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
                {
                    DirectorySearcher dSearcher = new DirectorySearcher(dEntry);
                    dSearcher.Filter = "(&(objectClass=user)(samAccountName=" + parser["samaccountname"] + "))";
                    dSearcher.PropertiesToLoad.AddRange(requiredProperties);

                    var output = new Dictionary<string, string>();

                    var results = dSearcher.FindOne();

                    if (results == null)
                    {
                        throw new Exception("Could not find user with samaccountname " + parser["samaccountname"]);
                    }

                    foreach (var result in results.Properties.PropertyNames)
                    {
                        output.Add((string)result, results.Properties[(string)result][0].ToString());

                        // Validate if Home Directory Exist
                        if ((string)result == "homedirectory")
                            if (Directory.Exists(results.Properties[(string)result][0].ToString()))
                                output.Add("HomeFolderExistsYN", "Y");
                            else
                                output.Add("HomeFolderExistsYN", "N");
                    }

                    var orderedOutput = requiredProperties
                        .Select(key => output.ContainsKey(key.ToLower()) ? new KeyValuePair<string, string>(key, output[key.ToLower()] ?? "") : new KeyValuePair<string, string>(key, ""))
                        .Union(output.Where(kvp => !requiredProperties.Select(rp => rp.ToLower()).Contains(kvp.Key.ToLower()))
                                     .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value ?? "")))
                        .Where(kvp => requiredProperties.Select(rp => rp.ToLower()).Contains(kvp.Key.ToLower()))
                        .OrderBy(kvp => kvp.Key)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    if (orderedOutput.ContainsKey("CanonicalName"))
                    {
                        string currentValue = orderedOutput["CanonicalName"];
                        string newValue = currentValue.Substring(currentValue.IndexOf('/') + 1, currentValue.LastIndexOf('/') - currentValue.IndexOf('/') - 1);
                        newValue = newValue.Substring(0, newValue.LastIndexOf('/'));
                        orderedOutput["CanonicalName"] = newValue;
                    }


                    using (var writer = new StreamWriter("ADEmployee.csv"))
                    {
                        writer.WriteLine(string.Join("\t", orderedOutput.Select(x => x.Key)));
                        writer.WriteLine(string.Join("\t", orderedOutput.Select(x => x.Value)));
                    }

                }

                return 0;

            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Invalid argument: " + ex.ParamName);
                return -1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return -1;
            }
        }
    }
}
