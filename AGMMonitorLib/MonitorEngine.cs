using AGMMonitor.Models;
using HPAGMRestAPIWrapper;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AGMMonitor
{
    public class MonitorEngine
    {
        public MonitorEnum.ItemType Type { get; set; }
        public List<ConditionField> ItemCondition { get; set; }
        public List<ConditionField> BackLogItemCondition { get; set; }
        public List<NotificationField> NotificationFields { get; set; }
        public bool IsQueryBacklogItemFirst { get; set; }
        public List<History> ItemHistories { get; set; }
        public List<CheckPoint> CheckPoints { get; set; }
        public Dictionary<object, string> Results { get; set; }

        private AGMMonitorServer _server;

        public MonitorEngine(AGMMonitorServer server)
        {
            _server = server;
        }

        public void Run()
        {
            List<object> results = new List<object>();
            List<AGMField> searchFields = buildSearchFields(Type.ToString().ToLower());
            List<AGMField> returnFields = buildReturnFields(Type.ToString().ToLower());
            Dictionary<object, string> resultEntities = new Dictionary<object, string>();

            if (Type == MonitorEnum.ItemType.Defect)
                results = getDefects(searchFields, returnFields);
            else if (Type == MonitorEnum.ItemType.Requirement)
                results = getRequirements(searchFields, returnFields);

            if (results.Count > 0)
            {
                Results = getFilteredResults(results, Type);
            }
        }


        #region Private methods
        private DateTime convertByTimeZone(DateTime time)
        {
            DateTime returnTime = new DateTime();

            TimeZoneInfo tzInfo = TimeZoneInfo.FindSystemTimeZoneById(_server.TimeZone);
            returnTime = TimeZoneInfo.ConvertTimeFromUtc(time, tzInfo);            

            return returnTime.AddHours(_server.TimeZoneOffSet);
        }
        private DateTime getDateTimeByOperate(MonitorEnum.Operation operate, string value)
        {
            DateTime expectedDateTime = new DateTime();
            
            switch (operate)
            {
                case MonitorEnum.Operation.LastDays:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddDays(-Convert.ToInt32(value));
                    break;
                case MonitorEnum.Operation.LastWeeks:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddDays(-Convert.ToInt32(value) * 7);
                    break;
                case MonitorEnum.Operation.LastMonths:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddMonths(-Convert.ToInt32(value));
                    break;
                case MonitorEnum.Operation.LastYears:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddYears(-Convert.ToInt32(value));
                    break;
                case MonitorEnum.Operation.LastHours:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddHours(-Convert.ToInt32(value));
                    break;
                case MonitorEnum.Operation.LastMinutes:
                    expectedDateTime = convertByTimeZone(DateTime.UtcNow).AddMinutes(-Convert.ToInt32(value));                    
                    break;
            }

            return expectedDateTime;
        }
        private string StripHTML(string inputString)
        {
            return Regex.Replace(inputString, Constant.HTML_TAG_PATTERN, string.Empty);
        }

        private string getResultFromCheckPoints(object result, MonitorEnum.ItemType type)
        {
            bool isDetectInvalid = false;
            StringBuilder message = new StringBuilder();

            if (CheckPoints == null)
                return string.Empty;

            foreach (CheckPoint checkpoint in CheckPoints)
            {
                isDetectInvalid = false;
                string fieldValue = string.Empty;
                string endDate = null;

                switch(type)
                {
                    case MonitorEnum.ItemType.Defect:
                        AGMDefect bug = result as AGMDefect;
                        if (!checkpoint.IsBacklogItem)
                        {
                            fieldValue = bug.GetField(checkpoint.Field).Value;
                        }
                        else
                        {
                            fieldValue = bug.BacklogItem.GetField(checkpoint.Field).Value;
                        }
                        if (bug.BacklogItem.Release != null)
                            endDate = bug.BacklogItem.Release.EndDate;
                        break;
                    case MonitorEnum.ItemType.Requirement:
                        AGMRequirement req = result as AGMRequirement;
                        if (!checkpoint.IsBacklogItem)
                        {
                            fieldValue = req.GetField(checkpoint.Field).Value;
                        }
                        else
                        {
                            fieldValue = req.BacklogItem.GetField(checkpoint.Field).Value;
                        }
                        if (req.BacklogItem.Release != null)
                            endDate = req.BacklogItem.Release.EndDate;
                        break;
                }

                fieldValue = fieldValue.Replace("\n", string.Empty).Replace("\r", string.Empty);
                if (checkpoint.IsText)
                    fieldValue = StripHTML(fieldValue);

                switch (checkpoint.Operate)
                {
                    case MonitorEnum.Operation.Contain:
                        if (fieldValue.IndexOf(checkpoint.Value) < 0)
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.NotContain:
                        if (fieldValue.IndexOf(checkpoint.Value) > -1)
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.Equals:
                        if (!fieldValue.Equals(checkpoint.Value, StringComparison.InvariantCultureIgnoreCase))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.NotEquals:
                        if (fieldValue.Equals(checkpoint.Value, StringComparison.InvariantCultureIgnoreCase))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.LengthSmallerThan:
                        if (fieldValue.Length > Convert.ToInt32(checkpoint.Value))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.LengthLargerThan:
                        if (fieldValue.Length < Convert.ToInt32(checkpoint.Value))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.SmallerThan:
                        if (Convert.ToInt32(fieldValue) > Convert.ToInt32(checkpoint.Value))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.LargerThan:
                        if (Convert.ToInt32(fieldValue) < Convert.ToInt32(checkpoint.Value))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.LastYears:
                    case MonitorEnum.Operation.LastMonths:
                    case MonitorEnum.Operation.LastWeeks:
                    case MonitorEnum.Operation.LastDays:
                    case MonitorEnum.Operation.LastHours:
                    case MonitorEnum.Operation.LastMinutes:
                        if (getDateTimeByOperate(checkpoint.Operate, checkpoint.Value) > DateTime.Parse(fieldValue))
                            isDetectInvalid = true;
                        break;
                    case MonitorEnum.Operation.NotPast:
                        if (!string.IsNullOrEmpty(endDate))
                        {
                            if (DateTime.Parse(endDate) < convertByTimeZone(DateTime.UtcNow))
                                isDetectInvalid = true;
                        }                        
                        break;
                }
                if (isDetectInvalid)
                    message.Append(string.Format("<li>{0}</li>", checkpoint.Error));
            }

            return message.ToString();
        }

        private string getEndDate(string field, string value)
        { 
            if (field.Equals("release", StringComparison.InvariantCultureIgnoreCase))
            {
                AGMReleases rels = new AGMReleases(_server.Connection);
                var rel = rels.Get(Convert.ToInt32(value), null, null);
                return rel.GetField("end-date").Value;
            }
            else if(field.Equals("release-cycles", StringComparison.InvariantCultureIgnoreCase))
            {
                AGMReleaseCycles relcycles = new AGMReleaseCycles(_server.Connection);
                var relcycle = relcycles.Get(Convert.ToInt32(value), null, null);
                return relcycle.GetField("end-date").Value;
            }

            return null;
        }

        private Dictionary<object, string> getFilteredResults(List<object> results, MonitorEnum.ItemType type)
        {
            Dictionary<object, string> returnObjs = new Dictionary<object, string>();

            foreach (object result in results)
            {
                bool isValidResult = false;
                if (ItemHistories.Count() > 0)
                {
                    if (isMatchHistory(result, type))
                        isValidResult = true;
                    else
                        isValidResult = false;
                }
                else
                {
                    isValidResult = true;
                }
                

                if (isValidResult)
                {
                    if (CheckPoints.Count > 0)
                    {
                        string msg = getResultFromCheckPoints(result, type);
                        if (!string.IsNullOrEmpty(msg))
                            returnObjs.Add(result, msg);
                    }
                    else
                    {
                        returnObjs.Add(result, null);
                    }                                        
                }
            }

            return returnObjs;
        }

        private bool filterHistoryCondition(List<AGMAudit> audits, List<HistoryCondition> hisConditions)
        {
            bool isMatch = true;

            foreach(HistoryCondition hc in hisConditions)
            {
                switch(hc.Field)
                {
                    case "Time":
                        DateTime expectedDateTime = getDateTimeByOperate(hc.Operate, hc.Value);
                        if (audits.Where(o=> o.Time >= expectedDateTime).Count() > 0)
                            audits = audits.Where(o=> o.Time >= expectedDateTime).ToList<AGMAudit>();
                        else
                            return false;
                        break;
                    case "Action":
                        switch(hc.Operate)
                        {
                            case MonitorEnum.Operation.Equals:
                                if (audits.Where(o=> o.Action.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0)
                                    audits = audits.Where(o=> o.Action.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                            case MonitorEnum.Operation.NotEquals:
                                if (audits.Where(o=> o.Action.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0)
                                    audits = audits.Where(o=> o.Action.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                        }                        
                        break;
                    case "User":
                        switch(hc.Operate)
                        {
                            case MonitorEnum.Operation.Equals:
                                if (audits.Where(o=> o.User.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0)
                                    audits = audits.Where(o=> o.User.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                            case MonitorEnum.Operation.NotEquals:
                                if (audits.Where(o=> o.User.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0)
                                    audits = audits.Where(o=> o.User.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                        } 
                        break;
                    case "NewValue":
                        switch(hc.Operate)
                        {
                            case MonitorEnum.Operation.Equals:
                                if (audits.Where(o=> o.Properties.Where(v=>v.NewValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).Count() > 0)
                                    audits = audits.Where(o=> o.Properties.Where(v=>v.NewValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                            case MonitorEnum.Operation.NotEquals:
                                if (audits.Where(o=> o.Properties.Where(v=>v.NewValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0).Count() == 0)
                                    audits = audits.Where(o=> o.Properties.Where(v=>v.NewValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                        }
                        break;
                    case "OldValue":
                        switch(hc.Operate)
                        {
                            case MonitorEnum.Operation.Equals:
                                if (audits.Where(o=> o.Properties.Where(v=>v.OldValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).Count() > 0)
                                    audits = audits.Where(o=> o.Properties.Where(v=>v.OldValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                            case MonitorEnum.Operation.NotEquals:
                                if (audits.Where(o=> o.Properties.Where(v=>v.OldValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0).Count() == 0)
                                    audits = audits.Where(o=> o.Properties.Where(v=>v.OldValue.Equals(hc.Value, StringComparison.InvariantCultureIgnoreCase)).Count() == 0).ToList<AGMAudit>();
                                else
                                    return false;
                                break;
                        }
                        break;
                }
            }

            return isMatch;
        }

        private bool isMatchHistory(object obj, MonitorEnum.ItemType type)
        {
            bool isMatch = true;

            #region Get audits for entity
            List<AGMAudit> entityAudits = new List<AGMAudit>();
            List<AGMAudit> entityBacklogItemAudits = new List<AGMAudit>();
            switch(type)
            {
                case MonitorEnum.ItemType.Defect:
                    entityAudits = (obj as AGMDefect).Audits;
                    entityBacklogItemAudits = (obj as AGMDefect).BacklogItem.Audits;
                    break;
                case MonitorEnum.ItemType.Requirement:
                    entityAudits = (obj as AGMRequirement).Audits;
                    entityBacklogItemAudits = (obj as AGMRequirement).BacklogItem.Audits;
                    break;
            }
            #endregion

            #region Handle entity field history first
            foreach (History history in ItemHistories.Where(o=>!o.IsBacklogItem))
            {
                if (entityAudits.Where(o=>o.ParentType.Equals(type.ToString().ToLower()) && 
                    o.Properties.Where(p=>p.Label.Equals(history.Field, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).Count() == 0 )
                {
                    return false;
                }
                else
                {
                    List<AGMAudit> filteredAudits = 
                        entityAudits.Where(o=>o.ParentType.Equals(type.ToString().ToLower()) && 
                        o.Properties.Where(p=>p.Label.Equals(history.Field, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).ToList<AGMAudit>();

                    if (!filterHistoryCondition(filteredAudits, history.HistoryConditions))
                        return false;
                }
            }
            #endregion

            #region Handle entity backlogitem field history second
            foreach (History history in ItemHistories.Where(o=>o.IsBacklogItem))
            {
                if (entityBacklogItemAudits.Where(o=>o.ParentType.Equals(Constant.BackLogItemEntityName) && 
                    o.Properties.Where(p=>p.Label.Equals(history.Field, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).Count() == 0 )
                {
                    return false;
                }
                else
                {
                    List<AGMAudit> filteredAudits = 
                        entityBacklogItemAudits.Where(o=>o.ParentType.Equals(Constant.BackLogItemEntityName) && 
                        o.Properties.Where(p=>p.Label.Equals(history.Field, StringComparison.InvariantCultureIgnoreCase)).Count() > 0).ToList<AGMAudit>();

                    if (!filterHistoryCondition(filteredAudits, history.HistoryConditions))
                        return false;
                }
            }
            #endregion

            return isMatch;
        }

        private List<object> getDefects(List<AGMField> queryFields, List<AGMField> returnFields)
        {
            AGMDefects entities = new AGMDefects(_server.Connection);
           List<AGMDefect> entityList = entities.Search(queryFields, returnFields, null);

            if (entityList != null)
                return entityList.ToList<object>();

            return null;
        }
        private List<object> getRequirements(List<AGMField> fields, List<AGMField> returnFields)
        {
            AGMRequirements entities = new AGMRequirements(_server.Connection);
            List<AGMRequirement> entityList = entities.Search(fields, returnFields, null);

            if (entityList != null)
                return entityList.ToList<object>();

            return null;
        }
        private List<AGMField> buildSearchFields(string entityName)
        {
            List<AGMField> fields = new List<AGMField>();
            foreach (ConditionField itemField in ItemCondition)
            {
                string value = getFieldValue(itemField);

                fields.Add(new AGMField { Name = itemField.FieldName, Value = value, Entity = entityName });
            }
            foreach (ConditionField itemField in BackLogItemCondition)
            {
                string value = getFieldValue(itemField);

                fields.Add(new AGMField { Name = itemField.FieldName, Value = value, Entity = Constant.BackLogItemEntityName });
            }

            return fields;
        }
        private List<AGMField> buildReturnFields(string entityName)
        {
            List<AGMField> fields = new List<AGMField>();
            foreach (ConditionField itemField in ItemCondition)
            {
                fields.Add(new AGMField { Name = itemField.FieldName, Entity = entityName });
            }
            foreach (ConditionField itemField in BackLogItemCondition)
            {
                fields.Add(new AGMField { Name = itemField.FieldName, Entity = Constant.BackLogItemEntityName });
            }
            foreach (CheckPoint checkpoint in CheckPoints)
            {
                if (checkpoint.IsBacklogItem)
                {
                    if (fields.Where(o => o.Name.Equals(checkpoint.Field, StringComparison.InvariantCultureIgnoreCase) && o.Entity.Equals(Constant.BackLogItemEntityName)).Count() == 0)
                        fields.Add(new AGMField { Name = checkpoint.Field, Entity = Constant.BackLogItemEntityName });
                }
                else
                {
                    if (fields.Where(o => o.Name.Equals(checkpoint.Field, StringComparison.InvariantCultureIgnoreCase) && !o.Entity.Equals(Constant.BackLogItemEntityName)).Count() == 0)
                        fields.Add(new AGMField { Name = checkpoint.Field, Entity = entityName });
                }
            }
            foreach (NotificationField nfield in NotificationFields)
            {
                if (nfield.IsBackLogItem)
                {
                    if (fields.Where(o => o.Name.Equals(nfield.Name, StringComparison.InvariantCultureIgnoreCase) && o.Entity.Equals(Constant.BackLogItemEntityName)).Count() == 0)
                        fields.Add(new AGMField { Name = nfield.Name, Entity = Constant.BackLogItemEntityName });
                }
                else
                {
                    if (fields.Where(o => o.Name.Equals(nfield.Name, StringComparison.InvariantCultureIgnoreCase) && !o.Entity.Equals(Constant.BackLogItemEntityName)).Count() == 0)
                        fields.Add(new AGMField { Name = nfield.Name, Entity = entityName });
                }
            }
            return fields;
        }

        private string getFieldValue(ConditionField field)
        {
            string value = field.FieldValue;

            switch (field.Operate)
            {
                case MonitorEnum.Operation.Contain:
                    value = string.Format("'*{0}*'", value);
                    break;
                case MonitorEnum.Operation.NotContain:
                    value = string.Format("Not '*{0}*'", value);
                    break;
                case MonitorEnum.Operation.Equals:
                    value = string.IsNullOrEmpty(value) ? string.Format("\"\"") : value;
                    break;
                case MonitorEnum.Operation.NotEquals:
                    value = string.Format("Not '{0}'", value);
                    break;
                case MonitorEnum.Operation.SmallerThan:
                    value = string.Format("<{0}", value);
                    break;
                case MonitorEnum.Operation.LargerThan:
                    value = string.Format(">{0}", value);
                    break;
                case MonitorEnum.Operation.LastDays:
                case MonitorEnum.Operation.LastWeeks:
                case MonitorEnum.Operation.LastMonths:
                case MonitorEnum.Operation.LastYears:
                case MonitorEnum.Operation.LastHours:
                case MonitorEnum.Operation.LastMinutes:
                    value = string.Format(">='{0}'", getDateTimeByOperate(field.Operate, value).ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
            }

            return value;
        }
        
        #endregion
    }
}
