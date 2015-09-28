using System;
using System.Xml.Serialization;

namespace EpicorConsole.Utils
{
    public class Time
    {
        private DateTime timeEntryDate;

        [XmlAttribute]
        public string action { get; set; }

        public string TimeID { get; set; }
        public string TimeGUID { get; set; }
        public string ProjectCode { get; set; }
        public string TaskUID { get; set; }
        public string ActivityCode { get; set; }
        public string ResourceID { get; set; }

        public DateTime TimeEntryDate
        {
            get { return DateTime.SpecifyKind(timeEntryDate, DateTimeKind.Unspecified); }
            set { timeEntryDate = value; }
        }

        public string StatusCode { get; set; }
        public decimal StandardHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public string InvoiceComment { get; set; }
        public string StatusComment { get; set; }
        public string WorkComment { get; set; }
        public string Status { set; get; }
        public string TaskName { get; set; }
        public string ActivityDesc { get; set; }
        public string RowCheckFlag { get; set; }
        public int InternalFlag { get; set; }
        public string OriginalTimeID { get; set; }
        public string ProjectSiteURN { get; set; }
        public string ResourceSiteURN { get; set; }
        public string RemoteTimeID { get; set; }
        public string ProjectCustomer { get; set; }
        public string TimeTypeCode { get; set; }
        public string BatchID { get; set; }
        public string ProjectName { get; set; }
        public string TransactionIndex { get; set; }
        public string EventCode { get; set; }
        public string EventStatusCode { get; set; }
        public string StatusFlag { get; set; }
        public string LocationCode { get; set; }
        public string LocationDesc { get; set; }
        public string CreateUserID { get; set; }
        public string CreateDate { get; set; }
        public string LastUpdateUserID { get; set; }
        public string LastUpdateDate { get; set; }
        public string OrganizationID { get; set; }
        public string OriginFlag { set; get; }
        public string ResourceLongName { get; set; }
        public string Meal { get; set; }
        public string Travel { get; set; }
        public int UnassignedEntryFlag { get; set; }
        public decimal Hours { get; set; }
        public string WorkTypeCode { get; set; }
        public int LocRequiredTimeEntryFlag { get; set; }
        public string ProjectStatus { get; set; }
        public string OpportunityID { get; set; }
        public string Favorite { set; get; }
        public string TimeEntryComment { get; set; }
        public int TaskRuleNotesFlag { get; set; }
        public string RemoteProjectCode { get; set; }
        public string ProjectSiteName { get; set; }
        public string AvoidUpdateSite { get; set; }

        public string TaskOrActivity
        {
            get { return TaskUID == "-1" ? ActivityDesc : TaskName; }
        }

        public string ShortDate
        {
            get { return TimeEntryDate.ToString("MM/dd ddd"); }
        }

        public string ProjectNameOrCode
        {
            get { return TaskUID == "-1" ? ProjectCode : ProjectName; }
        }

        public override string ToString()
        {
            if (TaskUID == "-2")
                return ActivityDesc;

            return TimeEntryDate.ToShortDateString() + ": " + Hours + " " + ProjectName + " " + TaskOrActivity + " (" +
                   Status + ")";
        }

        public string StatusIcon
        {
            get
            {
                switch (StatusCode)
                {
                    case "E":
                        return "";
                    case "A":
                        return "";
                }
                return string.Empty;
            }
        }
    }
}