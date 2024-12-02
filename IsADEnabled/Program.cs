using ParametersParser;
using System;
using System.DirectoryServices;

namespace IsADEnabled
{
    internal class Program
    {
        static int Main(string[] args)
        {
            ParameterParser parser = new ParameterParser("Check AD User Account Status", "This command line tool checks the status of an Active Directory User Account");
            parser.Parameter("samaccountname", ParameterParser.AllowType.Required, "AD Sam Account Name");

            try
            {
                parser.Parse(args);

                var accountStatus = AccountStatusChecker.IsProfileEnabled(parser["samaccountname"]);
                if (!accountStatus.HasValue)  
                {
                    Console.Write("NotFound");
                    return 2; // Specific exit code for "user not found"
                }

                Console.Write(accountStatus.Value ? "Enabled" : "Disabled");
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

        public static class AccountStatusChecker
        {
            private static readonly string ldapPath = "LDAP://HW3-DC02.prosperitybank.com";
            private static readonly string username = "proc-ADWriter";
            private static readonly string password = "RzxH@8y9&eNUhw$B*m)M0aDMG";

            public static bool? IsProfileEnabled(string samAccountName)
            {
                try
                {
                    using (DirectoryEntry entry = new DirectoryEntry(ldapPath, username, password))
                    {
                        DirectorySearcher searcher = new DirectorySearcher(entry)
                        {
                            Filter = $"(&(objectClass=user)(samAccountName={samAccountName}))"
                        };
                        searcher.PropertiesToLoad.Add("userAccountControl");

                        var result = searcher.FindOne();
                        if (result == null)
                        {
                            return null;  
                        }

                        if (result.Properties.Contains("userAccountControl"))
                        {
                            int flags = (int)result.Properties["userAccountControl"][0];
                            return (flags & 0x2) == 0;
                        }
                        else
                        {
                            return null;  
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Failed to check user status.", ex);
                }
            }
        }
    }
}