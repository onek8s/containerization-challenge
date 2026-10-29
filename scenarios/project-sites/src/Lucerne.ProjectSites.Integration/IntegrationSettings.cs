using System;

namespace Lucerne.ProjectSites.Integration
{
    public class ErpSettings
    {
        /// <summary>e.g. https://erp01.corp.local/ProjectAccounting/ProjectService.svc</summary>
        public string Endpoint { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int PageSize { get; set; } = 200;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    }

    public class HrSettings
    {
        /// <summary>e.g. https://hr01.corp.local/hrapi</summary>
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public int PageSize { get; set; } = 100;
    }

    public class DocumentControlSettings
    {
        /// <summary>e.g. https://doccontrol.corp.local</summary>
        public string BaseUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
}
