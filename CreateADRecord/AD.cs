using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
