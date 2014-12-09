using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor.Models
{
    public class NotificationModel
    {
        public List<string> Fields;
        public List<NotificationItem> Items;
    }

    public class NotificationItem
    {
        public List<NotificationField> ItemFields;
        public string CheckPointsMessage;
    }
}
