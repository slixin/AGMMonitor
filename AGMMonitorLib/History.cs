using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class History
    {
        public string Field { get; set; }
        public bool IsBacklogItem { get; set; }
        public List<HistoryCondition> HistoryConditions { get; set; }
    }
    public class HistoryCondition
    {
        public string Field { get; set; }
        public MonitorEnum.Operation Operate { get; set; }
        public string Value { get; set; }
    }
}
