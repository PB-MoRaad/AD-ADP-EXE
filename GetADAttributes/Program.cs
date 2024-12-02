using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetADAttributes
{
    class Program
    {
        static void Main(string[] args)
        {
            string ldapPath = "LDAP://HW3-DC02.prosperitybank.com";
            string serviceAccountUsername = "proc-ADWriter";
            string serviceAccountPassword = "RzxH@8y9&eNUhw$B*m)M0aDMG";

            // Connect to the domain controller using a service account
            DirectoryEntry rootDSE = new DirectoryEntry(ldapPath + "/RootDSE", serviceAccountUsername, serviceAccountPassword);

            // Get the schema naming context
            string schemaDN = rootDSE.Properties["schemaNamingContext"].Value.ToString();
            DirectoryEntry schemaEntry = new DirectoryEntry($"LDAP://{rootDSE.Properties["dnsHostName"].Value}/{schemaDN}", serviceAccountUsername, serviceAccountPassword);

            // Search for attributeSchema objects
            DirectorySearcher searcher = new DirectorySearcher(schemaEntry, "(objectClass=attributeSchema)");
            SearchResultCollection results = searcher.FindAll();

            // Extract the attribute names from the search results
            List<string> attributeNames = new List<string>();
            foreach (SearchResult result in results)
            {
                DirectoryEntry entry = result.GetDirectoryEntry();
                string attributeName = entry.Properties["lDAPDisplayName"].Value.ToString();
                attributeNames.Add(attributeName);
            }

            // Print the attribute names
            Console.WriteLine("Active Directory Attributes:");
            foreach (string attributeName in attributeNames)
            {
                Console.WriteLine(attributeName);
            }
            Console.ReadLine();
            rootDSE.Dispose();
            schemaEntry.Dispose();
            searcher.Dispose();
        }    
    }
}
