using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace ADSandbox
{
    class Program
    {
        static void Main(string[] args)
        {

            ADUsers.GetADUsers();

            ADTree Root = null;

            System.Threading.Tasks.Task BuildOUStructure = System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                Root = new ADTree("LDAP://OU=Domain Users,DC=prosperitybank,DC=com", "proc-ADWriter", "RzxH@8y9&eNUhw$B*m)M0aDMG");
            });

            BuildOUStructure.Wait();

        
            var json = JsonConvert.SerializeObject(Root);

            Console.ReadLine();
    
            }
    }

 
}
