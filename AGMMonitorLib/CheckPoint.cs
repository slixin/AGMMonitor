using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class CheckPoint
    {
        public string Field { get; set; }
        public bool IsBacklogItem { get; set; }
        public bool IsText { get; set; }
        public MonitorEnum.Operation Operate { get; set; }
        public string Value { get; set; }
        public string Error { get; set; }
    }
}
