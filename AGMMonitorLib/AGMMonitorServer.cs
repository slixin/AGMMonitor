using HPAGMRestAPIWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class AGMMonitorServer
    {
        public string URL { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string Project { get; set; }
        public string TimeZone { get; set; }
        public int TimeZoneOffSet { get; set; }
        public AGMConnection Connection { get; set; }

        public AGMMonitorServer(){}

        public void Connect()
        {
            try
            {
                Console.WriteLine("Trying to connect Server {0}", URL);
                Connection = new AGMConnection(URL, UserName, Password, Domain, Project, true);
                Console.WriteLine("Connected");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        ~AGMMonitorServer()
        {
            if (Connection != null)
                Connection.Logoff();
        }
    }
}
