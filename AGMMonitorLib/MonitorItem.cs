using AGMMonitor.Models;
using HPAGMRestAPIWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AGMMonitor
{
    public class MonitorItem
    {
        #region Properties
        public AGMMonitorServer Server { get; set; }
        public MailService MailNotifyService { get; set; }
        public string GUID { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public int DurationMinutes { get; set; }
        public MonitorEnum.ItemType ItemType { get; set; }
        public List<ConditionField> ItemCondition { get; set; }
        public List<ConditionField> BackLogItemCondition { get; set; }
        public bool IsQueryBacklogItemFirst { get; set; }
        public List<History> ItemHistories { get; set; }
        public List<CheckPoint> CheckPoints { get; set; }
        public Notification Notification { get; set; }
        public MonitorEnum.ItemStatus Status { get; set; }
        public string PrefixURL { get; set; }
        public Logger Log { get; set; }
        public System.Timers.Timer ItemTimer { get; set; }
        public long LastMonitorTime { get; set; }
        public string ConfigurationFile { get; set; }
        #endregion

        #region Private members
        private bool isTimerStarted { get; set; }
        #endregion

        #region Public methods
        public MonitorItem()
        {
        }
        public void StartItemMonitor()
        {
            bool isSuccess = false;
            string ignoreItemStr = null;
            Log.LogPath = System.IO.Path.Combine(System.IO.Path.Combine(Environment.CurrentDirectory, "Log"), string.Format("{0}_{1}.log", Server.Project, DateTime.UtcNow.ToString("yyyy-MM-dd")));

            if (!Enabled)
                return;
            
            
            if (LastMonitorTime != 0)
            {
                double passedMins = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - LastMonitorTime).TotalMinutes;
                if (passedMins < DurationMinutes)
                {
                    if (!isTimerStarted)
                        StartItemTimer();
                    ignoreItemStr = string.Format("Item: {0}\r\nNext monitor in  {1} minutes.", Description, DurationMinutes - passedMins);
                    Log.WriteLine(string.Format("Item: {0}", Description), 0);
                    Log.WriteLine(string.Format("Type: {0}", ItemType), 0);
                    Log.WriteLine(string.Format("Next monitor in  {0} minutes.", DurationMinutes - passedMins), 0);
                    return;
                }
            }
            
            Status = MonitorEnum.ItemStatus.Monitoring;            
            var worker = new BackgroundWorker();
            worker.DoWork += (s, ee) =>
            {
                int retry = 3;                

                while(retry > 0 && !isSuccess)
                {
                    try
                    {
                        StringBuilder itemMonitorLog = new StringBuilder();
                        itemMonitorLog.AppendLine(string.Format("Item: {0}", Description));
                        itemMonitorLog.AppendLine(string.Format("Type: {0}", ItemType.ToString()));
                        Server.Connect();
                        if (Server.Connection != null)
                        {
                            MonitorEngine me = new MonitorEngine(Server);
                            me.ItemCondition = ItemCondition;
                            me.BackLogItemCondition = BackLogItemCondition;
                            me.NotificationFields = Notification.Fields;
                            me.ItemHistories = ItemHistories;
                            me.CheckPoints = CheckPoints;
                            me.IsQueryBacklogItemFirst = IsQueryBacklogItemFirst;
                            me.Type = ItemType;
                            me.Run();

                            if (me.Results != null)
                            {
                                if (me.Results.Count > 0)
                                {
                                    var notifyModel = buildNotificationModel(me.Results, ItemType);
                                    var to = buildReceivers(me.Results, ItemType, Notification.To);
                                    var cc = buildReceivers(me.Results, ItemType, Notification.CC);
                                    notify(notifyModel, to, cc);
                                    itemMonitorLog.AppendLine(string.Format("NOTIFIED! TO:[{0}] CC:[{1}]", to, cc));
                                }
                                else
                                {
                                    itemMonitorLog.AppendLine("**** NO RESULT! ****"); 
                                }
                            }
                            else
                            {
                                itemMonitorLog.AppendLine("**** NO RESULT! ****"); 
                            }

                            isSuccess = true;                            
                        }    
                        else
                        {
                            itemMonitorLog.AppendLine(string.Format("Server '{0}' connect fail!", Server.URL));
                        }

                        if (isSuccess)
                        {
                            Log.WriteLine(itemMonitorLog.ToString(), 0);
                        }
                        else
                        {
                            Log.WriteLine(itemMonitorLog.ToString(), 2);
                            Alert.AlertMe(MailNotifyService, itemMonitorLog.ToString(), MailNotifyService.BCC);
                        }
                    }
                    catch(Exception ex)
                    {
                        Alert.AlertMe(MailNotifyService, ex.Message + ex.StackTrace, MailNotifyService.BCC);
                        retry--;
                    }                    
                }                
            };

            worker.RunWorkerCompleted += (s, ee) =>
            {
                Status = MonitorEnum.ItemStatus.Normal;
                if (isSuccess)
                {
                    long lastMonitorTime = DateTime.UtcNow.Ticks;
                    LastMonitorTime = lastMonitorTime;
                    UpdateLastMonitorTimeInXml(lastMonitorTime);
                }
                if (!isTimerStarted)
                    StartItemTimer();
            };

            worker.RunWorkerAsync();
        }

        public void StopItemMonitor()
        {
            if (ItemTimer != null)
                ItemTimer.Stop();
            isTimerStarted = false;
        }
        #endregion

        #region Private methods
        private void UpdateLastMonitorTimeInXml(long lastMonitorTime)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(ConfigurationFile);
                XmlNode node = doc.SelectSingleNode(string.Format("AGMMonitorService/Monitor/Items/Item[@id='{0}']", GUID));
                if (node != null)
                {
                    if (node.Attributes["last_monitor_time"] ==  null)
                    {
                        XmlAttribute attr = doc.CreateAttribute("last_monitor_time");
                        attr.Value = lastMonitorTime.ToString();                        
                        node.Attributes.Append(attr);
                    }
                    else
                    {
                        node.Attributes["last_monitor_time"].Value = lastMonitorTime.ToString();
                    }
                    doc.Save(ConfigurationFile);
                }                
            }
            catch{}
        }

        private string buildReceivers(Dictionary<object, string> results, MonitorEnum.ItemType type, string receiver)
        {
            string ret = string.Empty;

            if (!string.IsNullOrEmpty(receiver))
            {
                if (receiver.IndexOf("@") > 0)
                    ret = receiver;
                else
                {
                    switch (type)
                    {
                        case MonitorEnum.ItemType.Defect:
                            foreach (AGMDefect bug in results.Select(o => (o.Key as AGMDefect)))
                            {
                                var field = bug.GetField(receiver);
                                if (field == null)
                                    field = bug.BacklogItem.GetField(receiver);

                                if (ret.IndexOf(field.Value) < 0)
                                    ret += string.Format("{0},", field.Value);
                            }
                            break;
                        case MonitorEnum.ItemType.Requirement:
                            foreach (AGMRequirement req in results.Select(o => (o.Key as AGMRequirement)))
                            {
                                var field = req.GetField(receiver);
                                if (field == null)
                                    field = req.BacklogItem.GetField(receiver);

                                if (ret.IndexOf(field.Value) < 0)
                                    ret += string.Format("{0},", field.Value);
                            }
                            break;
                    }
                }

                if (ret.Substring(ret.Length - 1).Equals(","))
                    ret = ret.Substring(0, ret.Length - 1);
            }

            return ret;
        }

        private void notify(NotificationModel notifyModel, string to, string cc)
        {
            SendObject sendObj = new SendObject();
            
            try
            {
                sendObj.To = to;
                sendObj.CC = cc;
                sendObj.Title = Notification.Subject;
                sendObj.Body = RazorEngine.Razor.Parse(File.ReadAllText(System.IO.Path.Combine(Environment.CurrentDirectory, Notification.Template)), notifyModel);
                MailNotifyService.SendNotification(sendObj);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private Dictionary<object, string> sorting(Dictionary<object, string> results, MonitorEnum.ItemType type, NotificationField field)
        {
            Dictionary<object, string> sorted = new Dictionary<object, string>();
            MonitorEnum.Sort sort = (MonitorEnum.Sort)Enum.Parse(typeof(MonitorEnum.Sort), field.OrderBy);

            switch(type)
            {
                case MonitorEnum.ItemType.Defect:                    
                    if (sort == MonitorEnum.Sort.ASC)
                    {
                        sorted = results.OrderBy(o => (o.Key as AGMDefect).GetField(field.Name).Value).ToDictionary(k => k.Key, k => k.Value);
                    }
                    else
                    {
                        sorted = results.OrderByDescending(o => (o.Key as AGMDefect).GetField(field.Name).Value).ToDictionary(k => k.Key, k => k.Value);
                    }
                    break;
                case MonitorEnum.ItemType.Requirement:
                    if (sort == MonitorEnum.Sort.ASC)
                    {
                        sorted = results.OrderBy(o => (o.Key as AGMRequirement).GetField(field.Name).Value).ToDictionary(k => k.Key, k => k.Value);
                    }
                    else
                    {
                        sorted = results.OrderByDescending(o => (o.Key as AGMRequirement).GetField(field.Name).Value).ToDictionary(k => k.Key, k => k.Value);
                    }
                    break;
            }

            return sorted;
        }

        private NotificationModel buildNotificationModel(Dictionary<object, string> results, MonitorEnum.ItemType type)
        {
            NotificationModel nm = new NotificationModel();
            Dictionary<object, string> sorted = results;
            nm.Fields = Notification.Fields.Select(o => o.Name).ToList<string>();
            nm.Items = new List<NotificationItem>();

            if (Notification.Fields.Where(o => !string.IsNullOrEmpty(o.OrderBy)).Count() > 0)
                sorted = sorting(results, type, Notification.Fields.Where(o => !string.IsNullOrEmpty(o.OrderBy)).Single());

            foreach (KeyValuePair<object, string> result in sorted)
            {
                NotificationItem ni = new NotificationItem();
                ni.CheckPointsMessage = result.Value;

                ni.ItemFields = new List<NotificationField>();

                foreach (NotificationField nf in Notification.Fields)
                {
                    var value = getItemFieldValue(result.Key, type, nf);
                    NotificationField newNF = new NotificationField()
                    {
                        Name = nf.Name,
                        IsBackLogItem = nf.IsBackLogItem,
                        OrderBy = nf.OrderBy,
                        Value = value,
                        Href = string.IsNullOrEmpty(nf.Href) ? string.Empty : string.Format("{0}{1}",PrefixURL, nf.Href.Replace("%fieldvalue%", value)),
                    };

                    ni.ItemFields.Add(newNF);
                }
                nm.Items.Add(ni);
            }

            return nm;
        }

        private string getItemFieldValue(object item, MonitorEnum.ItemType type, NotificationField nf)
        {
            string value = null;
            switch(type)
            {
                case MonitorEnum.ItemType.Defect:
                    AGMDefect bug = item as AGMDefect;
                    if (!nf.IsBackLogItem)
                    {
                        value = bug.GetField(nf.Name).Value;
                    }                        
                    else
                    {
                        value = bug.BacklogItem.GetField(nf.Name).Value;
                        if (value != null)
                        {
                            if (nf.Name.Equals("release", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = bug.BacklogItem.Release.Name;
                            }
                            else if (nf.Name.Equals("sprint", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = bug.BacklogItem.ReleaseCycle.Name;
                            }
                            else if (nf.Name.Equals("application", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = bug.BacklogItem.Application.Name;
                            }
                        }
                    }                        
                    break;
                case MonitorEnum.ItemType.Requirement:
                    AGMRequirement req = item as AGMRequirement;
                    if (!nf.IsBackLogItem)
                    {
                        value = req.GetField(nf.Name).Value;
                    } 
                    else
                    {
                        value = req.BacklogItem.GetField(nf.Name).Value;
                        if (value != null)
                        {
                            if (nf.Name.Equals("release", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = req.BacklogItem.Release.Name;
                            }
                            else if (nf.Name.Equals("sprint", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = req.BacklogItem.ReleaseCycle.Name;
                            }
                            else if (nf.Name.Equals("application", StringComparison.InvariantCultureIgnoreCase))
                            {
                                value = req.BacklogItem.Application.Name;
                            }
                        }
                    }    
                    break;
            }

            return value;
        }
        
        #region Timer
        private void ItemTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            while(Status == MonitorEnum.ItemStatus.Monitoring)
            {
                System.Threading.Thread.Sleep(1000);
            }
            
            StartItemMonitor();
        }

        private void StartItemTimer()
        {
            ItemTimer = new System.Timers.Timer();
            ItemTimer.AutoReset = true;
            ItemTimer.Elapsed += new System.Timers.ElapsedEventHandler(ItemTimer_Elapsed);
            ItemTimer.Interval = TimeSpan.FromMinutes(DurationMinutes).TotalMilliseconds;
            ItemTimer.Start();
            isTimerStarted = true;
        }
        #endregion
        #endregion
    }
}
