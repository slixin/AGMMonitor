using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class Logger
    {
        /// <summary>
        /// 0 - text log, 1 - event log
        /// </summary>
        public int LoggerType { get; set; }
        public string LogPath { get; set; }
        public string EventSourceName { get; set; }

        private System.Diagnostics.EventLog logEvent;

        public Logger()
        {
        }

        public void Init()
        {
            if (!System.Diagnostics.EventLog.SourceExists(EventSourceName))
                System.Diagnostics.EventLog.CreateEventSource(EventSourceName, "Application");

            logEvent = new System.Diagnostics.EventLog();
            logEvent.Source = EventSourceName;
        }

        /// <summary>
        /// Write log
        /// </summary>
        /// <param name="message">context of the log</param>
        /// <param name="level">0 - info, 1 - warning, 2 - error</param>
        public void WriteLine(string message, int level)
        {
            if (LoggerType == 0)
            {
                using(StreamWriter sw = new StreamWriter(LogPath, true))
                {
                    string prefix = string.Format("{0} ",DateTime.UtcNow.ToString());

                    switch(level)
                    {
                        case 0:
                            prefix += "[INFO]:";
                            break;
                        case 1:
                            prefix += "[WARN]:";
                            break;
                        case 2:
                            prefix += "[ERROR]:";
                            break;
                    }
                    string output = string.Format("{0}{1}", prefix, message);
                    sw.WriteLine(output);
                    sw.Flush();
                    sw.Close();
                    Console.WriteLine(output);
                }
            }
            else
            {
                System.Diagnostics.EventLogEntryType eveType = new System.Diagnostics.EventLogEntryType();

                switch(level)
                {
                    case 0:
                        eveType = System.Diagnostics.EventLogEntryType.Information;
                        break;
                    case 1:
                        eveType = System.Diagnostics.EventLogEntryType.Warning;
                        break;
                    case 2:
                        eveType = System.Diagnostics.EventLogEntryType.Error;
                        break;
                }
                logEvent.WriteEntry(message, eveType);
                Console.WriteLine(message);
            }
        }
    }
}
