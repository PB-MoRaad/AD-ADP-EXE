using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;

namespace ADSandbox
{

    public class ADTree
    {
        List<ADTree> childOUs = new List<ADTree>();
        private string _RootName = "Root";
      

        public DirectoryEntry RootOU { get; set; }
        public string RootDN { get; set; }
        public string RootName
        {
            get
            {
                return _RootName;
            }
            set
            {
                var RootArray = value.Split('=');
                _RootName = RootArray[1];
            }
        }

        public List<ADTree> ChildOUs
        {
            get { return childOUs; }
            set { childOUs = value; }
        }

        public ADTree(string dn, string user, string pwd)
        {
            RootOU = new DirectoryEntry(dn, user, pwd);
            RootDN = dn;
            BuildADTree().Wait();
        }

        public ADTree(DirectoryEntry root)
        {
            RootOU = root;
            RootDN = root.Path;
            RootName = root.Name;
            BuildADTree().Wait();
        }

        private Task BuildADTree()
        {
            return Task.Factory.StartNew(() =>
            {
                object locker = new object();
                Parallel.ForEach(RootOU.Children.Cast<DirectoryEntry>().AsEnumerable(), child =>
                {
                    if (child.SchemaClassName.Equals("organizationalUnit"))
                    {
                        ADTree ChildTree = new ADTree(child);
                        lock (locker)
                        {
                            ChildOUs.Add(ChildTree);
                        }
                    }
                });
            });
        }
    }
}
