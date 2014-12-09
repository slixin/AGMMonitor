using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class AGMMonitor
    {
        public string Name { get; set; }
        public AGMMonitorServer Server { get; set; }
        public List<MonitorItem> Items { get; set; }
    }
}
