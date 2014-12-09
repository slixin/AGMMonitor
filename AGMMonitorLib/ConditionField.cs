using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class ConditionField
    {
        public string FieldName { get; set; }
        public MonitorEnum.Operation Operate { get; set; }
        public string FieldValue { get; set; }
    }
}
