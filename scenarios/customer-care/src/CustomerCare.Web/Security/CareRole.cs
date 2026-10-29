using System.Configuration;

namespace Tailspin.CustomerCare.Web.Security
{
    public enum CareRole
    {
        Agent,
        Supervisor,
        Administrator
    }

    /// <summary>Maps the application roles to the Active Directory groups configured in appSettings.</summary>
    public static class CareGroups
    {
        public static string GroupFor(CareRole role)
        {
            switch (role)
            {
                case CareRole.Administrator:
                    return ConfigurationManager.AppSettings["CustomerCare:AdminGroup"] ?? @"CORP\CC-Admins";
                case CareRole.Supervisor:
                    return ConfigurationManager.AppSettings["CustomerCare:SupervisorGroup"] ?? @"CORP\CC-Supervisors";
                default:
                    return ConfigurationManager.AppSettings["CustomerCare:AgentGroup"] ?? @"CORP\CC-Agents";
            }
        }
    }
}
