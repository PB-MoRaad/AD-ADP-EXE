using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GetADEmployees
{
    class Program
    {
        static int Main(string[] args)
        {
            var Employees = new List<Employee>();
            try
            {
                using (DirectoryEntry dEntry = new DirectoryEntry("LDAP://HW3-DC02.prosperitybank.com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
                {
                    DirectorySearcher dSearcher = new DirectorySearcher(dEntry);

                    //dSearcher.Filter = "(&(objectCategory=person)(objectClass=user)(!userAccountControl:1.2.840.113556.1.4.803:=2))";
                    dSearcher.Filter = "(&(objectCategory=person)(objectClass=user))";


                    string[] requiredProperties = new string[]
                                 {
                                    "company", "department", "description", "displayName", "mail", "employeeID", "mailNickname", "manager", "name", "physicalDeliveryOfficeName",
                                     "SamAccountName", "title", "userPrincipalName", "distinguishedName", "msExchShadowProxyAddresses", "msRTCSIPPrimaryUserAddress", "proxyAddresses",
                                     "targetAddress", "l", "st", "streetAddress", "postalCode", "c", "co", "countryCode", "sn", "givenName", "homeDirectory", "scriptPath", "ou", "cn",
                                     "telephoneNumber"
                                 };


                    foreach (string property in requiredProperties)
                        dSearcher.PropertiesToLoad.Add(property);

                    using (var results = dSearcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            var entry = result.GetDirectoryEntry();
                            bool isEnabled = false;
                            if (entry.Properties["userAccountControl"].Value != null)
                            {
                                isEnabled = ((int)entry.Properties["userAccountControl"].Value & 2) == 0;
                            }

                            var Employee = new Employee()
                            {
                                Company = GetPropertyValue(result, "company"),
                                Department = GetPropertyValue(result, "department"),
                                Description = GetPropertyValue(result, "description"),
                                DisplayName = GetPropertyValue(result, "displayName"),
                                EmailAddress = GetPropertyValue(result, "mail"),
                                EmployeeID = GetPropertyValue(result, "employeeID"),
                                mailNickname = GetPropertyValue(result, "mailNickname"),
                                Manager = GetPropertyValue(result, "manager"),
                                Name = GetPropertyValue(result, "name"),
                                physicalDeliveryOfficeName = GetPropertyValue(result, "physicalDeliveryOfficeName"),
                                SamAccountName = GetPropertyValue(result, "sAMAccountName"),
                                Title = GetPropertyValue(result, "title"),
                                UserPrincipalName = GetPropertyValue(result, "userPrincipalName"),
                                distinguishedName = GetPropertyValue(result, "distinguishedName"),
                                msExchShadowProxyAddresses = result.Properties.Contains("msExchShadowProxyAddresses") ? GetObjects((ResultPropertyValueCollection)result.Properties["msExchShadowProxyAddresses"]) : null,
                                msRTCSIPPrimaryUserAddress = result.Properties.Contains("msRTCSIP-PrimaryUserAddress") ? GetObjects((ResultPropertyValueCollection)result.Properties["msRTCSIP-PrimaryUserAddress"]) : null,
                                proxyAddresses = result.Properties.Contains("proxyAddresses") ? GetObjects((ResultPropertyValueCollection)result.Properties["proxyAddresses"]) : null,
                                targetAddress = result.Properties.Contains("targetAddress") ? GetObjects((ResultPropertyValueCollection)result.Properties["targetAddress"]) : null,
                                l = GetPropertyValue(result, "l"),
                                st = GetPropertyValue(result, "st"),
                                streetAddress = GetPropertyValue(result, "streetAddress")?.ToString()?.Replace("\r\n", " "),
                                postalCode = GetPropertyValue(result, "postalCode"),
                                c = GetPropertyValue(result, "c"),
                                co = GetPropertyValue(result, "co"),
                                countryCode = GetPropertyValue(result, "countryCode"),
                                sn = GetPropertyValue(result, "sn"),
                                givenName = GetPropertyValue(result, "givenName"),
                                homeDirectory = GetPropertyValue(result, "homeDirectory"),
                                scriptPath = GetPropertyValue(result, "scriptPath"),
                                ou = GetPropertyValue(result, "ou"),
                                cn = GetPropertyValue(result, "cn"),
                                telephoneNumber = GetPropertyValue(result, "telephoneNumber"),
                                Status = isEnabled ? "Enabled" : "Disabled"
                        };
                       
                            Employees.Add(Employee);
                        }
                    }

                    var csv = Employees.Select(x => string.Join("|", new string[] {
                        x.Company,
                        x.Department,
                        x.Description,
                        x.DisplayName,
                        x.EmailAddress,
                        x.EmployeeID,
                        x.mailNickname,
                        x.Manager,
                        x.Name,
                        x.physicalDeliveryOfficeName,
                        x.SamAccountName,
                        x.Title,
                        x.UserPrincipalName,
                        x.distinguishedName,
                        x.msRTCSIPPrimaryUserAddress,
                        x.msExchShadowProxyAddresses,
                        x.targetAddress,
                        x.proxyAddresses,
                        x.l,
                        x.st,
                        x.streetAddress,
                        x.postalCode,
                        x.c,
                        x.co,
                        x.countryCode,
                        x.sn,
                        x.givenName,
                        x.homeDirectory,
                        x.scriptPath,
                        x.ou,
                        x.cn,
                        x.telephoneNumber,
                        x.Status
                    })).ToList();

                    requiredProperties = requiredProperties.Concat(new[] { "Status" }).ToArray();
                    csv.Insert(0, string.Join("|", requiredProperties));                 
                    File.WriteAllLines("ADEmployees.csv", csv);

                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                int errorCode = ex.HResult;           
                return (errorCode == 0 ? -1 : errorCode);
            }
        }

        private static string GetPropertyValue(SearchResult entry, string propertyName)
        {
            if (entry.Properties.Contains(propertyName))
            {
                var attribute = entry.Properties[propertyName];
                return attribute.Count > 0 ? attribute[0].ToString() : null;
            }
            return null;
        }


        private static string GetObjects(ResultPropertyValueCollection objectCollection)
        {
            List<string> objectList = new List<string>();
            Regex cmdLineRegex = new Regex(@"^(?i)(smtp|sip)", RegexOptions.IgnoreCase);

            foreach (object obj in objectCollection)
            {
                string value = obj.ToString();
                Match cmdLineMatch = cmdLineRegex.Match(value);

                if (cmdLineMatch.Success)
                {
                    objectList.Add(value);
                }
            }

            return string.Join(",", objectList);
        }
    }
}
