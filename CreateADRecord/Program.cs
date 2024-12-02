using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using Newtonsoft.Json;

using ParametersParser;

namespace CreateADUser
{
    class Program
    {
        static int Main(string[] args)
        {
            ParameterParser parser = new ParameterParser("Create AD User Account Utility", "This command line tool creates Active Directory User Account");
            parser.Parameter("ldap", ParameterParser.AllowType.Required, "LDAP Connection String");
            parser.Parameter("json", ParameterParser.AllowType.Required, "JSON containing all AD Atributes");
            parser.Parameter("templateprofile", ParameterParser.AllowType.Required, "Template profile to copy group memberships from");
            parser.Parameter("password", ParameterParser.AllowType.Required, "New user default password");
            parser.Parameter("UPNFlag", ParameterParser.AllowType.Required, "UPN Flag");
            string samAccountName = string.Empty;

            try
            {
                parser.Parse(args);
                var originalDictionary = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(parser["json"]).FirstOrDefault();
                var ADAttributes = new Dictionary<string, string>(originalDictionary);
                string[] fieldsToRemove = { "NewHireID", "ProfileID", "cn", "userPrincipalName" };
                foreach (string field in fieldsToRemove)
                {
                    ADAttributes.Remove(field);
                }

                samAccountName = ADAttributes["saMAccountName"];
                string connection = "LDAP://" + parser["ldap"];
                //DeleteProfile(ADAttributes["saMAccountName"], connection);

                DirectoryEntry newUser;
                using (DirectoryEntry dEntry = new DirectoryEntry(connection, "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
                {
                    if (DoesUserExist(dEntry, samAccountName))
                    {
                        Console.Error.WriteLine($"Error: User with samaccount name {samAccountName} already exists");
                        return -1;
                    }

                    newUser = dEntry.Children.Add("CN=" + originalDictionary["cn"], "User");
                    foreach (var Attribute in ADAttributes)
                    {
                        AD.SetProperty(newUser, Attribute.Key, Attribute.Value);
                    }

                    if (parser["UPNFlag"] == "Yes")
                    {
                        string userPrincipalName = originalDictionary["userPrincipalName"];
                        if (!IsValidEmail(userPrincipalName))
                        {
                            Console.Error.WriteLine("Error: UPN is invalid");
                            return -1;
                        }

                        AD.SetProperty(newUser, "userPrincipalName", userPrincipalName);
                    }
                    
                    newUser.CommitChanges();
                    newUser.Properties["userAccountControl"].Value = 0x2;

                    // Enable Account & Set Password
                    //int val = (int)newUser.Properties["userAccountControl"].Value;
                    //newUser.Properties["userAccountControl"].Value = val & ~0x2;               
                    newUser.Invoke("SetPassword", new object[] { parser["password"] });

                    // Force user to change password at next login
                    newUser.Properties["pwdLastSet"].Value = 0;
                    newUser.CommitChanges();                  
                }

                // Get Memberships from Profile Template
                if (!string.IsNullOrEmpty(parser["templateprofile"]))
                {
                    var memberships = new List<string>();              
                    using (DirectoryEntry tEntry = new DirectoryEntry("LDAP://HW3-DC02.prosperitybank.com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
                    {
                        memberships = AD.GetGroups(tEntry, parser["templateprofile"]);
                    }

                    foreach (var membership in memberships)
                    {
                        AD.AddToGroup(newUser, membership);
                    }
                }

                newUser.Close();
                return 0;
            }
            catch (JsonReaderException)
            {
                Console.Error.WriteLine("Error: Invalid JSON format");
                return -1;
            }
            catch (DirectoryServicesCOMException dex)
            {
                Console.Error.WriteLine($"Error: User with samaccount name {samAccountName} AD creation failed. " +
                    $"Additional Technical Error: {dex.ToString()}");
                return -1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: User with samaccount name {samAccountName} AD creation failed. " +
                   $"Additional Technical Error: {ex.ToString()}");
                return -1;
            }
        }

        public static bool DoesUserExist(DirectoryEntry de, string user)
        {
            DirectorySearcher searcher = new DirectorySearcher(de);
            searcher.Filter = "(&(objectClass=user)(sAMAccountName=" + user + "))";
            var result = searcher.FindOne();
            if (result == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }
        #region TEST CODE / Delete Profile
        public static void DeleteProfile(string profileName, string connection)
        {
            using (var root = new DirectoryEntry(connection, "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
            {
                using (var searcher = new DirectorySearcher(root))
                {
                    searcher.Filter = $"(&(objectCategory=person)(objectClass=user)(saMAccountName=" + profileName + "))";

                    var record = searcher.FindOne();
                    if (record != null)
                    {
                        var t = record.GetDirectoryEntry();
                        t.DeleteTree();
                    }                   
                }
            }
        }
        #endregion


    }

}
