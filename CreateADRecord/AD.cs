using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Management.Automation;
using System.Security;

namespace CreateADUser
{
    public static class AD
    {
        public static bool UserExists(DirectoryEntry dEntry, string UserName)
        {
            DirectorySearcher deSearch = new DirectorySearcher();
            deSearch.SearchRoot = dEntry;
            deSearch.Filter = "(&(objectClass=user) (cn=" + UserName + "))";
            SearchResultCollection results = deSearch.FindAll();
            if (results.Count == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static void SetProperty(DirectoryEntry de, string PropertyName, string PropertyValue)
        {
            if (!string.IsNullOrEmpty(PropertyValue))
            {
                if (de.Properties.Contains(PropertyName))
                {
                    de.Properties[PropertyName][0] = PropertyValue;
                }
                else
                {
                    de.Properties[PropertyName].Add(PropertyValue);
                }
            }
        }

        public static List<String> GetGroups(DirectoryEntry tEntry, string username)
        {
            DirectorySearcher dSearcher = new DirectorySearcher(tEntry)
            {
                Filter = "(&(objectClass=user)(sAMAccountName=" + username + "))"
            };

            SearchResult result = dSearcher.FindOne();

            if (result != null)
            {
                DirectoryEntry user = result.GetDirectoryEntry();
                PropertyValueCollection memberOf = user.Properties["memberOf"];

                List<String> membersOf = new List<String>();
                foreach (object groupDN in memberOf)
                {
                    membersOf.Add(groupDN.ToString());
                }

                return membersOf;
            }
            return null;
        }

        public static void AddToGroup(DirectoryEntry userDn, string groupDn)
        {

            DirectoryEntry GrpEntry = new DirectoryEntry("LDAP://HW3-DC02.prosperitybank.com/" + groupDn, "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG");

            GrpEntry.Invoke("Add", new object[] { userDn.Path.ToString() });
            GrpEntry.CommitChanges();
            GrpEntry.Close();

        }

        public static void CreateProfileLocation(string homeDir, string accountName, string userName, string password)
        {
            try
            {
                // Create a SecureString for the password for security reasons
                SecureString securePassword = new SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }

                securePassword.MakeReadOnly();

                string arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{ConfigurationManager.AppSettings["scriptPath"]}\" " +
                        $"-userHomeDirectory \"{homeDir}\" " +
                        $"-userSamAccountName \"{accountName}\"";

                // Configure ProcessStartInfo to run as different user
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    Domain = "PROSPERITY",
                    UserName = userName,
                    Password = securePassword,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        Console.WriteLine("PowerShell Output:");
                        Console.WriteLine(output);
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell Errors:");
                        Console.WriteLine(error);
                    }

                    // Check exit code
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"PowerShell script failed with exit code: {process.ExitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateProfileCreation: {ex.Message}");
                throw ex;
            }
        }
    }
}
