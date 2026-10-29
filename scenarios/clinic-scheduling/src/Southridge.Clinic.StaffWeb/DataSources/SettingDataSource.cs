using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    /// <summary>Database settings (dbo.AppSetting) shared by the batch jobs, services and both web sites.</summary>
    [DataObject]
    public class SettingDataSource
    {
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public List<AppSettingRow> All()
        {
            return new SystemRepository().ListSettings();
        }

        [DataObjectMethod(DataObjectMethodType.Update, true)]
        public void Update(string name, string value)
        {
            new SystemRepository().SaveSetting(name, value ?? string.Empty, HttpContext.Current.User.Identity.Name);
        }
    }
}
