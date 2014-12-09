using AutomationHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AGMMonitor
{
    public class Configuration
    {
        public static AGMMonitorServer GetMonitorServer(XmlNode node)
        {
            XmlNode serverNode = node.SelectSingleNode("Server");
            return new AGMMonitorServer()
                        {
                            URL = serverNode.Attributes["url"].Value,
                            UserName = serverNode.Attributes["username"].Value,
                            Password = Encryption.Decrypt(serverNode.Attributes["password"].Value, GetProcessorSerial()),
                            Domain = serverNode.Attributes["domain"].Value,
                            Project = serverNode.Attributes["project"].Value,
                            TimeZone = serverNode.Attributes["timezone"].Value,
                            TimeZoneOffSet = Convert.ToInt32(serverNode.Attributes["timezoneoffset"].Value),
                        };
        }
        public static List<ConditionField> GetConditions(XmlNode node, string path)
        {
            List<ConditionField> fields = new List<ConditionField>();
            if (node.SelectNodes(path) != null)
            {
                foreach (XmlNode subnode in node.SelectNodes(path))
                {
                    // add new field
                    var newField = new ConditionField
                    {
                        FieldName = subnode.Attributes["field"].Value,
                        FieldValue = subnode.Attributes["value"].Value,
                        Operate = (MonitorEnum.Operation)Enum.Parse(typeof(MonitorEnum.Operation), subnode.Attributes["operate"].Value),
                    };
                    fields.Add(newField);
                }
            }

            return fields;
        }
        public static List<CheckPoint> GetCheckPoints(XmlNode node, string path)
        {
            List<CheckPoint> cps = new List<CheckPoint>();
            // Get CheckPoints
            foreach (XmlNode cpNode in node.SelectNodes(path))
            {
                CheckPoint newCP = new CheckPoint()
                {
                    Field = cpNode.Attributes["field"].Value,
                    IsBacklogItem = cpNode.Attributes["isbacklogitem"] == null ? false : Convert.ToBoolean(cpNode.Attributes["isbacklogitem"].Value),
                    Operate = (MonitorEnum.Operation)Enum.Parse(typeof(MonitorEnum.Operation), cpNode.Attributes["operate"].Value),
                    Value = cpNode.Attributes["value"].Value,
                    Error = cpNode.Attributes["error"].Value,
                    IsText = cpNode.Attributes["istext"] != null ? Convert.ToBoolean(cpNode.Attributes["istext"].Value) : false,
                };
                cps.Add(newCP);
            }

            return cps;
        }
        public static List<History> GetItemHistories(XmlNode node, string path)
        {
            List<History> histories = new List<History>();

            foreach (XmlNode hisNode in node.SelectNodes(path))
            {
                var hisCons = new List<HistoryCondition>();
                foreach (XmlNode hisConNode in hisNode.SelectNodes("Condition"))
                {
                    hisCons.Add(new HistoryCondition
                    {
                        Field = hisConNode.Attributes["field"].Value,
                        Operate = (MonitorEnum.Operation)Enum.Parse(typeof(MonitorEnum.Operation), hisConNode.Attributes["operate"].Value),
                        Value = hisConNode.Attributes["value"].Value,
                    });
                }
                histories.Add(new History
                {
                    Field = hisNode.Attributes["field"].Value,
                    IsBacklogItem = hisNode.Attributes["isbacklogitem"] == null ? false : Convert.ToBoolean(hisNode.Attributes["isbacklogitem"].Value),
                    HistoryConditions = hisCons
                });
            }

            return histories;
        }
        public static Notification GetItemNotification(XmlNode node, string path)
        {
            Notification notification = new Notification();
            XmlNode notificationNode = node.SelectSingleNode(path);

            if (notificationNode != null)
            {
                notification.Template = notificationNode.Attributes["template"].Value;
                notification.To = notificationNode.Attributes["to"].Value;
                notification.CC = notificationNode.Attributes["cc"] == null ? string.Empty : notificationNode.Attributes["cc"].Value;
                notification.Subject = notificationNode.Attributes["subject"].Value;
                notification.Fields = new List<NotificationField>();

                foreach (XmlNode nField in notificationNode.SelectNodes("Field"))
                {
                    notification.Fields.Add(new NotificationField
                    {
                        Name = nField.Attributes["name"].Value,
                        IsBackLogItem = nField.Attributes["isbacklogitem"] == null ? false : Convert.ToBoolean(nField.Attributes["isbacklogitem"].Value),
                        OrderBy = nField.Attributes["orderby"] == null ? string.Empty : nField.Attributes["orderby"].Value,
                        Href = nField.Attributes["href"] == null ? string.Empty : nField.Attributes["href"].Value,
                    });
                }
            }

            return notification;
        }

        private static string GetProcessorSerial()
        {
            string cpuInfo = string.Empty;
            ManagementClass cimobject = new ManagementClass("Win32_Processor");
            ManagementObjectCollection moc = cimobject.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                cpuInfo = mo.Properties["ProcessorId"].Value.ToString();
            }

            return cpuInfo;
        }
    }
}
