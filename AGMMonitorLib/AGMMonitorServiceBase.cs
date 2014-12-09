using AutomationHelper;
using HPAGMRestAPIWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace AGMMonitor
{    
    public class AGMMonitorServiceBase
    {
        #region Properties
        public List<AGMMonitor> Monitors { get; set; }        
        public MailService Mail { get; set; }
        #endregion

        #region Private members
        private Logger logger;
        private string configFile;
        private System.Timers.Timer mainTimer;
        private int timerDuration = 2;
        #endregion

        #region Constructor
        public AGMMonitorServiceBase(string file)
        {
            logger = new Logger();
            logger.EventSourceName = Constant.EventSourceName;

            configFile = file;

            if (System.IO.File.Exists(configFile))
            {
                Monitors = new List<AGMMonitor>();
                LoadMonitors(configFile);
            }
            else
            {
                throw new Exception(string.Format("The configuration file {0} is not exists.", configFile));
            }

            StartMainTimer();
        }
        #endregion

        #region Public Methods        
        public void Start()
        {
            foreach (AGMMonitor monitor in Monitors)
            {
                foreach (MonitorItem item in monitor.Items)
                {
                    item.Status = MonitorEnum.ItemStatus.Normal;
                    item.Server = monitor.Server;
                    item.MailNotifyService = Mail;
                    item.PrefixURL = string.Format("{0}/agm/webui/alm/{1}/{2}/apm/?TENANTID={3}",
                                monitor.Server.URL,
                                monitor.Server.Domain,
                                monitor.Server.Project,
                                monitor.Server.Domain.Substring(1, monitor.Server.Domain.IndexOf("_") - 1));
                    item.StartItemMonitor();
                    System.Threading.Thread.Sleep(5000);
                }
            }            
        }
        #endregion

        #region Private Methods
        private void StartMainTimer()
        {
            mainTimer = new System.Timers.Timer();
            mainTimer.AutoReset = true;
            mainTimer.Elapsed += new System.Timers.ElapsedEventHandler(mainTimer_Elapsed);
            mainTimer.Interval = TimeSpan.FromMinutes(timerDuration).TotalMilliseconds;
            mainTimer.Start();
        }
        private void mainTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateMonitor();
        }

        private void UpdateMonitor()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configFile);

                // Get Mail Service Info
                Mail = LoadMailService(doc);

                #region Update Monitor
                XmlNodeList monitorNodes = doc.SelectNodes("AGMMonitorService/Monitor");
                foreach (XmlNode monitorNode in monitorNodes)
                {
                    string monitorName = monitorNode.Attributes["name"].Value;
                    if (Monitors.Where(o => o.Name.Equals(monitorName)).Count() == 1)
                    {
                        AGMMonitor currentMonitor = Monitors.Where(o => o.Name.Equals(monitorName)).Single() as AGMMonitor;
                        currentMonitor.Server = Configuration.GetMonitorServer(monitorNode);
                        // Only get the node which isupdated
                        foreach (XmlNode itemNode in monitorNode.SelectNodes("Items/Item"))
                        {
                            string id = itemNode.Attributes["id"].Value;
                            // New item added.
                            if (currentMonitor.Items.Where(o => o.GUID.Equals(id)).Count() == 0)
                            {
                                var newItem = LoadMonitorItem(itemNode);
                                currentMonitor.Items.Add(newItem);
                                newItem.StartItemMonitor();                                
                            }
                            else // The item is already exists.
                            {
                                if (itemNode.Attributes["updated"] != null)
                                {
                                    // Need to be updated.
                                    if (itemNode.Attributes["updated"].Value.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        MonitorItem currentItem = currentMonitor.Items.Where(o => o.GUID.Equals(id)).Single() as MonitorItem;
                                        currentItem.StopItemMonitor();

                                        bool _IsQueryBacklogItemFirst = false;

                                        if (itemNode.SelectSingleNode("Condition/BackLogFields") != null)
                                        {
                                            if (itemNode.SelectSingleNode("Condition/BackLogFields").Attributes["prioritized"] != null)
                                                _IsQueryBacklogItemFirst = Convert.ToBoolean(itemNode.SelectSingleNode("Condition/BackLogFields").Attributes["prioritized"].Value);
                                        }

                                        // Get Monitor Item Condition
                                        var itemConditionFields = Configuration.GetConditions(itemNode, "Condition/ItemFields/Field");
                                        var backlogFields = Configuration.GetConditions(itemNode, "Condition/BackLogFields/Field");
                                        var checkPoints = Configuration.GetCheckPoints(itemNode, "CheckPoints/CheckPoint");
                                        var histories = Configuration.GetItemHistories(itemNode, "Condition/Histories/History");
                                        var notification = Configuration.GetItemNotification(itemNode, "Notification");

                                        currentItem.Enabled = Convert.ToBoolean(itemNode.Attributes["enabled"].Value);
                                        currentItem.Description = itemNode.Attributes["description"].Value;
                                        currentItem.DurationMinutes = Convert.ToInt32(itemNode.Attributes["duration_mins"].Value);
                                        currentItem.ItemType = (MonitorEnum.ItemType)Enum.Parse(typeof(MonitorEnum.ItemType), itemNode.Attributes["type"].Value);
                                        currentItem.ItemCondition = itemConditionFields;
                                        currentItem.BackLogItemCondition = backlogFields;
                                        currentItem.CheckPoints = checkPoints;
                                        currentItem.ItemHistories = histories;
                                        currentItem.Notification = notification;
                                        currentItem.IsQueryBacklogItemFirst = _IsQueryBacklogItemFirst;
                                        currentItem.Log = logger;
                                        currentItem.ConfigurationFile = configFile;
                                        itemNode.Attributes["updated"].Value = "false";
                                        currentItem.StartItemMonitor();
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                doc.Save(configFile);
            }
            catch (Exception ex)
            {
                Alert.AlertMe(Mail, string.Format("{0}\r\n{1}", ex.Message, ex.StackTrace), Mail.BCC);
            }
        }
        private void LoadMonitors(string file)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);

                logger.LoggerType = Convert.ToInt32(doc.SelectSingleNode("AGMMonitorService").Attributes["logtype"].Value);

                // Get Mail Service Info
                Mail = LoadMailService(doc);

                #region Get Monitor
                XmlNodeList monitorNodes = doc.SelectNodes("AGMMonitorService/Monitor");
                foreach (XmlNode monitorNode in monitorNodes)
                {
                    string monitorName = monitorNode.Attributes["name"].Value;                    
                    AGMMonitor agmMonitor = new AGMMonitor();
                    agmMonitor.Name = monitorName;
                    agmMonitor.Server = Configuration.GetMonitorServer(monitorNode);                    
                    agmMonitor.Items = new List<MonitorItem>();
                    foreach (XmlNode itemNode in monitorNode.SelectNodes("Items/Item"))
                    {
                        var newItem = LoadMonitorItem(itemNode);
                        agmMonitor.Items.Add(newItem);
                    }

                    Monitors.Add(agmMonitor);
                }
                #endregion
            }
            catch (Exception ex)
            {
                if (Mail != null)
                    Alert.AlertMe(Mail, string.Format("Error when loading configuration file: {0}", ex.Message), Mail.BCC);
                else
                    Console.WriteLine(ex.Message);
            }
        }
        private MailService LoadMailService(XmlDocument doc)
        {
            XmlNode mailServiceNode = doc.SelectSingleNode("AGMMonitorService/MailService");
            MailService mail = new MailService(
                Convert.ToBoolean(mailServiceNode.Attributes["isenabled"].Value),
                mailServiceNode.Attributes["server"].Value,
                Convert.ToInt32(mailServiceNode.Attributes["port"].Value),
                mailServiceNode.Attributes["from"].Value,
                mailServiceNode.Attributes["cc"].Value);

            return mail;
        }
        private MonitorItem LoadMonitorItem(XmlNode itemNode)
        {
            bool _IsQueryBacklogItemFirst = false;

            string guid = itemNode.Attributes["id"].Value;            

            if (itemNode.SelectSingleNode("Condition/BackLogFields") != null)
            {
                if (itemNode.SelectSingleNode("Condition/BackLogFields").Attributes["prioritized"] != null)
                    _IsQueryBacklogItemFirst = Convert.ToBoolean(itemNode.SelectSingleNode("Condition/BackLogFields").Attributes["prioritized"].Value);
            }

            // Get Monitor Item Condition
            var itemConditionFields = Configuration.GetConditions(itemNode, "Condition/ItemFields/Field");
            var backlogFields = Configuration.GetConditions(itemNode, "Condition/BackLogFields/Field");
            var checkPoints = Configuration.GetCheckPoints(itemNode, "CheckPoints/CheckPoint");
            var histories = Configuration.GetItemHistories(itemNode, "Condition/Histories/History");
            var notification = Configuration.GetItemNotification(itemNode, "Notification");

            MonitorItem newItem = new MonitorItem()
            {
                GUID = itemNode.Attributes["id"].Value,
                Enabled = Convert.ToBoolean(itemNode.Attributes["enabled"].Value),
                Description = itemNode.Attributes["description"].Value,
                DurationMinutes = Convert.ToInt32(itemNode.Attributes["duration_mins"].Value),
                ItemType = (MonitorEnum.ItemType)Enum.Parse(typeof(MonitorEnum.ItemType), itemNode.Attributes["type"].Value),
                ItemCondition = itemConditionFields,
                BackLogItemCondition = backlogFields,
                CheckPoints = checkPoints,
                ItemHistories = histories,
                Notification = notification,
                IsQueryBacklogItemFirst = _IsQueryBacklogItemFirst,
                Log = logger,
                ConfigurationFile = configFile,
                LastMonitorTime = itemNode.Attributes["last_monitor_time"] == null ? 0 : Convert.ToInt64(itemNode.Attributes["last_monitor_time"].Value),
            };

            return newItem;
        }
        
        #endregion
    }
}
