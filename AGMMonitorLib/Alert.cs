using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class Alert
    {
        public static void AlertMe(MailService mail, string message, string to)
        {
            Console.WriteLine(message);
            SendObject sd = new SendObject();
            sd.Title = "AGM Monitor Service Alert";
            sd.Body = message;
            sd.To = to;
            mail.SendNotification(sd);
        }
    }
}
