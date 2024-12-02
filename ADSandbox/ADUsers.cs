using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSandbox
{
    public static class ADUsers
    {
        public static void GetADUsers()
        {
            using (var root = new DirectoryEntry("LDAP://OU=Domain Users,DC=prosperitybank,DC=com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG"))
            {
                using (var searcher = new DirectorySearcher(root))
                {
                    // looking for a specific user
                    searcher.Filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName=mohamad.raad)(!userAccountControl:1.2.840.113556.1.4.803:=2))";

                    using (SearchResultCollection results = searcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            foreach (var propertyName in result.Properties.PropertyNames)
                            {
                                Console.WriteLine($"{propertyName} : {result.Properties[propertyName.ToString()][0]}");
                            }
                        }
                    }

                    // FYI, non-null results means the user was found
                    //var results = searcher.FindAll();

                    //var properties = results?.PropertiesLoaded;
                   // {
                        // ... iterate over all the groups the user is a member of
                    //}
                }
            }
            Console.ReadLine();
        }
    }
}
