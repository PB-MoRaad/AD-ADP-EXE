using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADPClientDemo
{
    public class AD
    {
        public void ADCall()
        {

            using (var context = new PrincipalContext(ContextType.Domain, "prosperity"))
            {

                using (var group = GroupPrincipal.FindByIdentity(context, "Domain Users"))
                {
                    if (group == null)
                    {
                        //MessageBox.Show("Group does not exist");
                    }
                    else
                    {
                        var users = group.GetMembers(true);
                        foreach (UserPrincipal user in users)
                        {
                            //user variable has the details about the user 
                        }
                    }
                }


                using (var searcher = new PrincipalSearcher(new UserPrincipal(context)))
                {
                    foreach (var result in searcher.FindAll())
                    {
                        DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;

                        foreach (var prop in de.Properties.PropertyNames)
                        {
                            Console.WriteLine(prop.ToString() + " : " + de.Properties[prop.ToString()].Value);
                        }
                        //Console.WriteLine("First Name: " + de.Properties["givenName"].Value);
                        //Console.WriteLine("Last Name : " + de.Properties["sn"].Value);
                        //Console.WriteLine("SAM account name   : " + de.Properties["samAccountName"].Value);
                        //Console.WriteLine("User principal name: " + de.Properties["userPrincipalName"].Value);
                        Console.WriteLine();
                    }
                }
            }
            Console.ReadLine();
        }
    }
}
