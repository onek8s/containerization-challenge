namespace Lucerne.ProjectSites.Contracts.Schema
{
    /// <summary>List titles. Must match the ListInstance elements in the farm solution features.</summary>
    public static class ListNames
    {
        // Hub site (projects.corp.local/sites/hub)
        public const string Projects = "Projects";
        public const string ProjectStatus = "Project Status";
        public const string SiteRequests = "Site Requests";
        public const string StaffDirectory = "Staff Directory";
        public const string RetentionLog = "Retention Log";
        public const string SyncLog = "Sync Log";

        // Project sites (projects.corp.local/projects/pNNNNN)
        public const string Deliverables = "Deliverables";
        public const string Risks = "Risks";
        public const string Rfis = "RFIs";
        public const string Actions = "Actions";
        public const string Team = "Project Team";
        public const string Hours = "Project Hours";
        public const string Minutes = "Meeting Minutes";
        public const string Transmittals = "Transmittals";
        public const string Settings = "Site Settings";
    }

    public static class WebProperties
    {
        public const string ProjectNumber = "lucerne_projectnumber";
        public const string ProjectStage = "lucerne_projectstage";
        public const string Office = "lucerne_office";
        public const string TemplateVersion = "lucerne_templateversion";
        public const string ProvisionedUtc = "lucerne_provisionedutc";
        public const string ClosedDate = "lucerne_closeddate";
        public const string HubUrl = "lucerne_huburl";

        /// <summary>Web application property holding the hub URL for timer jobs.</summary>
        public const string WebAppHubUrl = "lucerne.projectsites.huburl";
    }

    public static class ContentTypeIds
    {
        // 0x0101 Document + Lucerne prefix
        public const string EngineeringDocument = "0x010100B6A1F4D9C7E64C3B8F2E5A7D1C9B3E21";
        public const string Drawing = EngineeringDocument + "01";
        public const string Calculation = EngineeringDocument + "02";
        public const string Report = EngineeringDocument + "03";
        public const string Correspondence = EngineeringDocument + "04";

        // 0x01 Item + Lucerne prefix
        public const string Deliverable = "0x0100E3C2B1A0F94E4D6B8C7A5F3E2D1C0B9A01";
        public const string Risk = "0x0100E3C2B1A0F94E4D6B8C7A5F3E2D1C0B9A02";
        public const string Rfi = "0x0100E3C2B1A0F94E4D6B8C7A5F3E2D1C0B9A03";
        public const string ProjectRecord = "0x0100E3C2B1A0F94E4D6B8C7A5F3E2D1C0B9A10";
        public const string SiteRequest = "0x0100E3C2B1A0F94E4D6B8C7A5F3E2D1C0B9A11";
    }

    public static class TemplateNames
    {
        public const string FeatureId = "{4C7E3A52-91D8-4F0B-A6E1-2B9D58C0F713}";

        /// <summary>Web template from the Lucerne.ProjectSites.WebTemplate feature.</summary>
        public const string ProjectWebTemplate = FeatureId + "#LucerneProject";

        public const int CurrentVersion = 7;
    }
}
