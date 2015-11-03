using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EpicorConsole.Utils;
using EpicorLibrary.HelperService;
using EpicorLibrary.TimeService;
using EpicorLibrary.Utils;
using ICERequestHeader = EpicorLibrary.TimeService.ICERequestHeader;

namespace EpicorLibrary
{
    public enum TimeStates
    {
        New, Modified, Deleted
    }

    public class Epicor
    {
        public Epicor(string resourceId)
        {
            ResourceId = string.IsNullOrEmpty(resourceId) ? GetResourceId() : resourceId;
        }

        public string ResourceId { get; set; }

        public List<Activity> GetInternalActivities()
        {
            var client = new TimeWSSoapClient();

            XmlNode result;
            client.GetActiveActivities(new ICERequestHeader(), out result);

            var s = new XmlSerializer(typeof (Activity));
            return (from XmlNode node in result.ChildNodes
                select s.Deserialize(new StringReader(node.OuterXml)) as Activity).ToList();
        }

        public Tree<NavigatorNode> GetSiteActivities()
        {
            var client = new PSAClientHelperWSSoapClient();
            var startOfWeek = GetStartOfWeek(DateTime.Now.Date);
            var endOfWeek = startOfWeek.AddDays(7);
            var fromDate = startOfWeek.ToString("s", CultureInfo.InvariantCulture);
            var toDate = endOfWeek.ToString("s", CultureInfo.InvariantCulture);
            
            var criteriaDoc =
                string.Format(
                    "<ProjectTreeCriteriaDoc><SearchCriteria><TimeExpenseTreeType>T</TimeExpenseTreeType><DisplayTreeType>S</DisplayTreeType><ResourceID>{0}</ResourceID><ResourceSiteURN>E4SE</ResourceSiteURN><CustomerID></CustomerID><OpportunityOnlyCode></OpportunityOnlyCode><ProjectCodes></ProjectCodes><ProjectGroupCode></ProjectGroupCode><OrganizationID></OrganizationID><TaskSeqIDs/><WorkloadCode></WorkloadCode><SiteURN>E4SE</SiteURN><FavoritesTree>0</FavoritesTree><InternalTree>1</InternalTree><CustomTree>1</CustomTree><ResourceCategoryList>'3'</ResourceCategoryList><FromDate>{1}</FromDate><ToDate>{2}</ToDate></SearchCriteria></ProjectTreeCriteriaDoc>",
                    ResourceId, fromDate, toDate);

            XmlNode result;
            client.GetNavigator(new HelperService.ICERequestHeader(), criteriaDoc, out result);

            var s = new XmlSerializer(typeof (NavigatorNode));
            var nodes = new List<NavigatorNode>();

            foreach (XmlNode node in result.ChildNodes)
            {
                var nn = s.Deserialize(new StringReader(node.OuterXml)) as NavigatorNode;

                if (nn == null) continue;

                switch (nn.NodeType)
                {
                    case "Task":
                        nn.Data = (TaskData) node.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "Data").Select(
                            n => new XmlSerializer(typeof (TaskData)).Deserialize(new StringReader(n.OuterXml))).First();
                        break;
                    case "Project":
                        nn.Data = (ProjectData) node.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "Data").Select(
                            n => new XmlSerializer(typeof (ProjectData)).Deserialize(new StringReader(n.OuterXml)))
                            .First();
                        break;
                    case "InternalCode":
                        nn.Data =
                            (InternalCodeData) node.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "Data").Select(
                                n => new XmlSerializer(typeof (InternalCodeData)).Deserialize(new StringReader(n.OuterXml))).First();
                        break;
                    case "Internal":
                    case "Customer":
                    case "Site":
                        break;
                    default:
                        Console.WriteLine("Unknown Node Type: " + nn.NodeType);
                        break;
                }
                nodes.Add(nn);
            }

            var activitiesRoot = TranslateNodesIntoActivityTree(nodes);

            return activitiesRoot;
        }

        public string GetResourceId()
        {
            var windowsIdentity = WindowsIdentity.GetCurrent();
            
            if (windowsIdentity != null)
                return windowsIdentity.Name.Split('\\').Last().ToUpper();
            
            throw new Exception("Could not determine user identity");
        }

        public DateTime GetStartOfWeek(DateTime now)
        {
            return now.Date.AddDays(-(int) now.Date.DayOfWeek);
        }

        private Tree<NavigatorNode> TranslateNodesIntoActivityTree(IEnumerable<NavigatorNode> nodes)
        {
            var tree = new Tree<NavigatorNode>(new NavigatorNode
            {
                NodeType = "Root",
                Caption = "All Tasks",
                OutlineNumber = "0"
            });

            foreach (var node in nodes)
                GetParentOfNode(node, tree).AddChild(node);

            return tree;
        }

        private Tree<NavigatorNode> GetParentOfNode(NavigatorNode node, Tree<NavigatorNode> root)
        {
            var deepestMatch = root;
            var nodeOutlineParts = node.OutlineNumber.Split('.');

            root.Traverse(root, (possibleParentNode, level) =>
            {
                var parts = possibleParentNode.Data.OutlineNumber.Split('.');
                if (parts.Length != nodeOutlineParts.Length - 1) return;
                if (parts.SequenceEqual(nodeOutlineParts.Take(nodeOutlineParts.Length - 1)))
                {
                    deepestMatch = possibleParentNode;
                }
            });

            return deepestMatch;
        }

        public List<Time> GetChargesBetween(DateTime fromDate, DateTime toDate)
        {
            var client = new TimeWSSoapClient();

            XmlNode result;
            client.GetAllTimeEntries(new ICERequestHeader(), ResourceId, fromDate, toDate, fromDate, toDate,
                out result);

            var s = new XmlSerializer(typeof(Time));
            return (from XmlNode node in result.ChildNodes
                    select (Time)s.Deserialize(new StringReader(node.OuterXml))).ToList();
        }

        public List<Time> GetCurrentCharges(DateTime date)
        {
            var fromDate = GetStartOfWeek(date);
            var toDate = fromDate.AddDays(6);
            return GetChargesBetween(fromDate, toDate);
        }

        public void SaveTimes(IEnumerable times, TimeStates state)
        {
            var client = new TimeWSSoapClient();
            const string etcDoc = "<TimeTaskETCForProject useActionHints=\"true\"/>";

            var timeList = new StringBuilder("<TimeList ProxyResourceId=\"\" useActionHints=\"true\">");
            var s = new XmlSerializer(typeof (Time));
            var settings = new XmlWriterSettings { OmitXmlDeclaration = true };
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", "");
            
            var modifiedTimes = new List<Time>();

            if (state == TimeStates.New)
                modifiedTimes = ModifyTimesForNew(times);
            else if (state == TimeStates.Modified)
                modifiedTimes = ModifyTimesForApproval(times);
            else if (state == TimeStates.Deleted)
                modifiedTimes = ModifyTimesForDelete(times);

            foreach (var time in modifiedTimes)
            {
                var ms = new MemoryStream();
                using (var writer = XmlWriter.Create(ms, settings))
                {
                    s.Serialize(writer, time, namespaces);
                }

                ms.Seek(0, SeekOrigin.Begin);
                var xmlStr = new StreamReader(ms).ReadToEnd();
                timeList.Append(xmlStr);
            }

            timeList.Append("</TimeList>");

            bool result;
            XmlNode failResult;
            client.UpdateTimeAndTaskETCForTimeEntry(new ICERequestHeader(), timeList.ToString(), etcDoc, out result,
                out failResult);

            if (result && string.IsNullOrEmpty(failResult.InnerText)) return;

            var msg = "Failed to save times";
            if (!string.IsNullOrEmpty(failResult.InnerText))
                msg += ":\n" + failResult.InnerText;
            throw new Exception(msg);
        }

        private List<Time> ModifyTimesForNew(IEnumerable times)
        {
            var modifiedTimes = new List<Time>();

            foreach (Time time in times)
            {
                time.action = "newmodified";
                time.StatusCode = "N";
                time.Status = "New";
                modifiedTimes.Add(time);
            }

            return modifiedTimes;
        }

        public Time CreateTaskTime(DateTime date, TaskData tdata, string comments, string dayOfWeek, decimal hours)
        {
            return new Time
            {
                ProjectCode = tdata.ProjectCode,
                TaskUID = tdata.TaskUID,
                ResourceID = ResourceId,
                TimeEntryDate = GetStartOfWeek(date).AddDays((int) Enum.Parse(typeof (DayOfWeek), dayOfWeek)),
                StatusCode = "N",
                StandardHours = hours,
                OvertimeHours = 0m,
                Status = "New",
                TaskName = tdata.TaskName,
                InternalFlag = 0,
                ProjectSiteURN = "E4SE",
                ResourceSiteURN = "E4SE",
                ProjectCustomer = tdata.CustomerID,
                UnassignedEntryFlag = 0,
                Hours = hours,
                WorkTypeCode = "WORK",
                WorkComment = comments,
                LocRequiredTimeEntryFlag = 0,
                TaskRuleNotesFlag = 0,
                ProjectSiteName = "E4SE",
                ActivityDesc = string.Empty,
                ActivityCode = string.Empty,
                TimeID = string.Empty,
                TimeGUID = string.Empty,
                InvoiceComment = string.Empty,
                StatusComment = string.Empty,
                RowCheckFlag = string.Empty,
                OriginalTimeID = string.Empty,
                RemoteTimeID = string.Empty,
                TimeTypeCode = string.Empty,
                BatchID = string.Empty,
                ProjectName = string.Empty,
                TransactionIndex = string.Empty,
                EventCode = string.Empty,
                EventStatusCode = string.Empty,
                StatusFlag = string.Empty,
                LocationCode = string.Empty,
                LocationDesc = string.Empty,
                CreateUserID = string.Empty,
                CreateDate = string.Empty,
                LastUpdateUserID = string.Empty,
                LastUpdateDate = string.Empty,
                OrganizationID = string.Empty,
                OriginFlag = string.Empty,
                ResourceLongName = string.Empty,
                Meal = string.Empty,
                Travel = string.Empty,
                ProjectStatus = string.Empty,
                OpportunityID = string.Empty,
                Favorite = string.Empty,
                TimeEntryComment = string.Empty,
                RemoteProjectCode = string.Empty,
                AvoidUpdateSite = string.Empty
            };
        }

        public Time CreateInternalCodeTime(DateTime date, NavigatorNode node, string comments, string dayOfWeek, decimal hours)
        {
            var idata = (InternalCodeData) node.Data;

            return new Time
            {
                ProjectCode = "Internal Activities",
                TaskUID = "-1",
                ActivityCode = idata.ActivityCode,
                ActivityDesc = node.Caption,
                WorkComment = comments,
                ResourceID = ResourceId,
                TimeEntryDate = GetStartOfWeek(date).AddDays((int) Enum.Parse(typeof (DayOfWeek), dayOfWeek)),
                StatusCode = "N",
                Status = "New",
                StandardHours = hours,
                OvertimeHours = 0m,
                InternalFlag = 1,
                ProjectSiteURN = "E4SE",
                ResourceSiteURN = "E4SE",
                UnassignedEntryFlag = 0,
                Hours = hours,
                WorkTypeCode = "0",
                LocRequiredTimeEntryFlag = 0,
                TaskRuleNotesFlag = 0,
                ProjectSiteName = "E4SE",
                TimeID = string.Empty,
                TimeGUID = string.Empty,
                InvoiceComment = string.Empty,
                StatusComment = string.Empty,
                RowCheckFlag = string.Empty,
                OriginalTimeID = string.Empty,
                RemoteTimeID = string.Empty,
                TimeTypeCode = string.Empty,
                BatchID = string.Empty,
                ProjectName = string.Empty,
                TransactionIndex = string.Empty,
                EventCode = string.Empty,
                EventStatusCode = string.Empty,
                StatusFlag = string.Empty,
                LocationCode = string.Empty,
                LocationDesc = string.Empty,
                CreateUserID = string.Empty,
                CreateDate = string.Empty,
                LastUpdateUserID = string.Empty,
                LastUpdateDate = string.Empty,
                OrganizationID = string.Empty,
                OriginFlag = string.Empty,
                ResourceLongName = string.Empty,
                Meal = string.Empty,
                Travel = string.Empty,
                ProjectStatus = string.Empty,
                OpportunityID = string.Empty,
                Favorite = string.Empty,
                TimeEntryComment = string.Empty,
                RemoteProjectCode = string.Empty,
                AvoidUpdateSite = string.Empty
            };
        }

        public List<Time> ModifyTimesForApproval(IEnumerable times)
        {
            var modifiedTimes = new List<Time>();

            foreach (Time time in times)
            {
                time.action = "modified";
                time.StatusCode = "E";
                time.Status = "Ready for Approval";
                time.TimeGUID = string.Empty;
                time.InvoiceComment = time.InvoiceComment ?? string.Empty;
                time.StatusComment = time.StatusComment ?? string.Empty;
                time.WorkComment = time.WorkComment ?? string.Empty;
                time.TaskName = time.TaskName ?? string.Empty;
                time.RemoteTimeID = time.RemoteTimeID ?? string.Empty;
                time.ProjectCustomer = time.ProjectCustomer ?? string.Empty;
                time.TimeTypeCode = time.TimeTypeCode ?? string.Empty;
                time.BatchID = time.BatchID ?? string.Empty;
                time.ProjectName = time.ProjectName ?? string.Empty;
                time.TransactionIndex = time.TransactionIndex ?? string.Empty;
                time.EventCode = time.EventCode ?? string.Empty;
                time.EventStatusCode = time.EventStatusCode ?? string.Empty;
                time.LocationCode = time.LocationCode ?? string.Empty;
                time.LocationDesc = time.LocationDesc ?? string.Empty;
                time.OriginFlag = time.OriginFlag ?? string.Empty;
                time.Meal = time.Meal ?? string.Empty;
                time.Travel = time.Travel ?? string.Empty;
                time.WorkTypeCode = time.WorkTypeCode ?? string.Empty;
                time.ProjectStatus = time.ProjectStatus ?? string.Empty;
                time.OpportunityID = time.OpportunityID ?? string.Empty;
                time.Favorite = time.Favorite ?? string.Empty;
                time.TimeEntryComment = time.TimeEntryComment ?? string.Empty;
                time.RemoteProjectCode = time.RemoteProjectCode ?? string.Empty;

                modifiedTimes.Add(time);
            }

            return modifiedTimes;
        }

        public List<Time> ModifyTimesForDelete(IEnumerable times)
        {
            var modifiedTimes = new List<Time>();

            foreach (Time time in times)
            {
                time.action = "deleted";
                time.StatusCode = "N";
                time.Status = "Entered";
                time.TimeGUID = string.Empty;
                time.AvoidUpdateSite = "0";
                time.InvoiceComment = time.InvoiceComment ?? string.Empty;
                time.StatusComment = time.StatusComment ?? string.Empty;
                time.WorkComment = time.WorkComment ?? string.Empty;
                time.TaskName = time.TaskName ?? string.Empty;
                time.RemoteTimeID = time.RemoteTimeID ?? string.Empty;
                time.ProjectCustomer = time.ProjectCustomer ?? string.Empty;
                time.TimeTypeCode = time.TimeTypeCode ?? string.Empty;
                time.BatchID = time.BatchID ?? string.Empty;
                time.ProjectName = time.ProjectName ?? string.Empty;
                time.TransactionIndex = time.TransactionIndex ?? string.Empty;
                time.EventCode = time.EventCode ?? string.Empty;
                time.EventStatusCode = time.EventStatusCode ?? string.Empty;
                time.LocationCode = time.LocationCode ?? string.Empty;
                time.LocationDesc = time.LocationDesc ?? string.Empty;
                time.OriginFlag = time.OriginFlag ?? string.Empty;
                time.Meal = time.Meal ?? string.Empty;
                time.Travel = time.Travel ?? string.Empty;
                time.WorkTypeCode = time.WorkTypeCode ?? string.Empty;
                time.ProjectStatus = time.ProjectStatus ?? string.Empty;
                time.OpportunityID = time.OpportunityID ?? string.Empty;
                time.Favorite = time.Favorite ?? string.Empty;
                time.TimeEntryComment = time.TimeEntryComment ?? string.Empty;
                time.RemoteProjectCode = time.RemoteProjectCode ?? string.Empty;

                modifiedTimes.Add(time);
            }

            return modifiedTimes;
        }
        
    }
}