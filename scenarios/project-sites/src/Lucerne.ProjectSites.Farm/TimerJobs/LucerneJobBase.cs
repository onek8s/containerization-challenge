using System;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Farm.Common;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Farm.TimerJobs
{
    /// <summary>
    /// Base of the Lucerne timer jobs. Jobs are web application scoped and run once per farm (SPJobLockType.Job)
    /// on the server that runs the timer service instance for the web application.
    /// </summary>
    public abstract class LucerneJobBase : SPJobDefinition
    {
        protected LucerneJobBase()
        {
        }

        protected LucerneJobBase(string name, string title, SPWebApplication webApplication)
            : base(name, webApplication, null, SPJobLockType.Job)
        {
            Title = title;
        }

        protected IActivityLog Log
        {
            get { return new UlsActivityLog(Name); }
        }

        protected string HubUrl
        {
            get { return FarmSettings.HubUrl(WebApplication); }
        }

        public override void Execute(Guid targetInstanceId)
        {
            try
            {
                // Timer jobs run as the farm account; no elevation needed.
                Run(new SsomSiteConnector(false));
            }
            catch (Exception ex)
            {
                Log.Error(Title + " failed", ex);
                throw;
            }
        }

        protected abstract void Run(SsomSiteConnector connector);
    }
}
