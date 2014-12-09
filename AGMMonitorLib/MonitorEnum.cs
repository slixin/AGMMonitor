using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class MonitorEnum
    {
        public enum ItemType 
        { 
            Defect, 
            Requirement 
        }
        public enum Operation 
        { 
            LengthSmallerThan, 
            LengthLargerThan, 
            SmallerThan, 
            LargerThan, 
            Contain, 
            NotContain,
            Equals, 
            NotEquals,             
            LastYears,
            LastMonths,
            LastWeeks,
            LastDays,
            LastHours,
            LastMinutes,
            NotPast,
        }
        public enum ItemStatus 
        { 
            Normal,
            Monitoring, 
            Pause 
        }

        public enum Sort
        {
            ASC,
            DESC,
        }
    }
}
