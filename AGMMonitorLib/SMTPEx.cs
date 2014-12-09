using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace AGMMonitor
{
    public class SMTPEx
    {
        public string SMTPServer { get; set; }
        public int SMTPPort { get; set; }
        public bool EnableSSL { get; set; }
        public string Subject { get; set; }
        public string SMTPUsername { get; set; }
        public string SMTPPassword { get; set; }
        public string To { get; set; }
        public string Sender { get; set; }

        public string CcTo { get; set; }

        public string BccTo { get; set; }
        public string Attachments { get; set; }
        public string BodyText { get; set; }
        public string BodyHTML { get; set; }
        public Encoding BodyEncoding { get; set; }

        public int Priority { get; set; }

        private SmtpClient mailClient;

        public SMTPEx()
        {

        }

        public void Send()
        {
            try
            {
                mailClient = new SmtpClient(SMTPServer, SMTPPort);
                mailClient.Host = SMTPServer;
                if (!string.IsNullOrEmpty(SMTPUsername) && !string.IsNullOrEmpty(SMTPPassword))
                {
                    mailClient.UseDefaultCredentials = false;
                    mailClient.EnableSsl = true;
                    mailClient.Credentials = new NetworkCredential(SMTPUsername, SMTPPassword);
                }
                else
                {
                    mailClient.UseDefaultCredentials = true;
                }
                MailMessage message = new MailMessage();
                message.Sender = new MailAddress(Sender);
                message.From = new MailAddress(Sender);
                foreach (string t in To.Split(new char[] { ',' }))
                {
                    message.To.Add(new MailAddress(t.Trim()));
                }
                if (!string.IsNullOrEmpty(CcTo))
                {
                    foreach (string cct in CcTo.Split(new char[] { ',' }))
                    {
                        message.CC.Add(new MailAddress(cct.Trim()));
                    }
                }
                if (!string.IsNullOrEmpty(BccTo))
                {
                    foreach (string bcct in BccTo.Split(new char[] { ',' }))
                    {
                        message.Bcc.Add(new MailAddress(bcct.Trim()));
                    }
                }

                message.IsBodyHtml = !string.IsNullOrEmpty(BodyHTML);
                message.Priority = Priority == 0 ? MailPriority.Normal : (Priority == 1 ? MailPriority.High : MailPriority.Low);
                message.Subject = Subject;
                if (string.IsNullOrEmpty(BodyHTML))
                {
                    message.Body = BodyText;
                }
                else
                {
                    message.Body = BodyHTML;
                    message.BodyEncoding = BodyEncoding == null ? Encoding.Default : BodyEncoding;
                }
                if (!string.IsNullOrEmpty(Attachments))
                {
                    foreach (string att in Attachments.Split(new char[] { ',' }))
                    {
                        message.Attachments.Add(new Attachment(att));
                    }
                }
                mailClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                mailClient.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        public string BuildHTMLBody(string htmlTemplate, Dictionary<string, string> parameters)
        {
            string html = null;
            using (StreamReader sr = new StreamReader(htmlTemplate))
            {
                html = sr.ReadToEnd();

                foreach (KeyValuePair<string, string> kv in parameters)
                {
                    html = Regex.Replace(html, string.Format("%{0}%", kv.Key), kv.Value);
                }
            }

            return html;
        }
    }
}
