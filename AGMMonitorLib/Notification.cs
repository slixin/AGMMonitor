using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class Notification
    {
        public string Template { get; set; }
        public string To { get; set; }
        public string CC { get; set; }
        public string Subject { get; set; }
        public List<NotificationField> Fields { get; set; }
    }

    public class NotificationField
    {
        public string Name { get; set; }
        public string OrderBy { get; set; }
        public bool IsBackLogItem { get; set; }
        public string Value { get;set;}
        public string Href { get; set; }
    }
}
