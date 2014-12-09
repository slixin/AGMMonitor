using AGMMonitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitorConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string configfile = null;

            if (!System.IO.Directory.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, "Log")))
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Environment.CurrentDirectory, "Log"));

            if (args.Length == 0)
            {
                Console.WriteLine("Please point out the configuration file path.");
            }
            else if (!System.IO.File.Exists(configfile = args[0].Trim()))
            {
                if (!System.IO.File.Exists(configfile = System.IO.Path.Combine(Environment.CurrentDirectory, args[0].Trim())))
                    Console.WriteLine("The configuration file path is not existed.");
            }
            else
            {
                AGMMonitorServiceBase monitorServiceBase = new AGMMonitorServiceBase(configfile);
                monitorServiceBase.Start();
                Console.ReadLine();
            }

            
        }
    }
}
