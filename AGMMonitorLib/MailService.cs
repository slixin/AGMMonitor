using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class MailService
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string From { get; set; }
        public string BCC { get; set; }
        public bool IsEnabled { get; set;}

        private SMTPEx smtp;

        public MailService(bool isenabled, string server, int port, string from, string bcc)
        {
            IsEnabled = isenabled;
            Server = server;
            Port = port;
            From = from;
            BCC = bcc;
            smtp = new SMTPEx();
        }

        public void SendNotification(SendObject sendObject)
        {
            if (IsEnabled)
            {
                smtp.SMTPServer = Server;
                smtp.SMTPPort = Port;
                smtp.Sender = From;
                smtp.CcTo = sendObject.CC;
                smtp.BccTo = BCC;
                smtp.To = sendObject.To;
                smtp.Subject = sendObject.Title;
                smtp.BodyHTML = sendObject.Body;

                smtp.Send();
            }
        }
    }
}
