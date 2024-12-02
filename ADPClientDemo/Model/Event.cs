using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADPClientDemo.Model
{
    public class Event
    {
        public string data { get; set; }
        public string eventID { get; set; }
        public string eventNameCode { get; set; }
        public string serviceCategoryCode { get; set; }
        public string eventStatusCode { get; set; }
        public string recordDateTime { get; set; }
        public string creationDateTime { get; set; }
        public string effectiveDateTime { get; set; }
        public string originator { get; set; }
        public string actor { get; set; }       
    }
}
