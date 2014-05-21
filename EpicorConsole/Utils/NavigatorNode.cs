using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace EpicorConsole.Utils
{
    [XmlRoot("Node")]
    [DataContract]
    [KnownType(typeof (InternalCodeData))]
    [KnownType(typeof (TaskData))]
    [KnownType(typeof (ProjectData))]
    public class NavigatorNode
    {
        [DataMember]
        public string OutlineNumber { get; set; }

        [DataMember]
        public string Caption { get; set; }

        [DataMember]
        public string Image { get; set; }

        [DataMember]
        public string SelectedImage { get; set; }

        [DataMember]
        public string Checkbox { get; set; }

        [DataMember]
        public string NodeType { get; set; }

        [XmlIgnore]
        [DataMember]
        public IData Data { get; set; }

        public override string ToString()
        {
            if (NodeType == "InternalCode")
                return ((InternalCodeData) Data).ActivityCode + " (Internal)";

            if (NodeType != "Task")
                return Caption;

            var data = (TaskData) Data;
            return string.Format("{0}: {1}", data.ProjectCode, data.TaskName);
        }
    }

    public interface IData
    {
    }

    [XmlRoot("Data")]
    [DataContract]
    public class InternalCodeData : IData
    {
        [DataMember]
        public string ActivityCode { get; set; }
    }

    [XmlRoot("Data")]
    [DataContract]
    public class ProjectData : IData
    {
        [DataMember]
        public string ProjectCode { get; set; }

        [DataMember]
        public string ProjectSiteURN { get; set; }

        [DataMember]
        public string CustomerName { get; set; }

        [DataMember]
        public string CustomerID { get; set; }

        [DataMember]
        public string UnassignedEntryFlag { get; set; }
    }

    [XmlRoot("Data")]
    [DataContract]
    public class TaskData : IData
    {
        [DataMember]
        public string ProjectCode { get; set; }

        [DataMember]
        public string ProjectSiteURN { get; set; }

        [DataMember]
        public string CustomerName { get; set; }

        [DataMember]
        public string CustomerID { get; set; }

        [DataMember]
        public string TaskUID { get; set; }

        [DataMember]
        public bool Enabled { get; set; }

        [DataMember]
        public string TaskName { get; set; }

        [DataMember]
        public string WorkTypeCode { get; set; }
    }
}