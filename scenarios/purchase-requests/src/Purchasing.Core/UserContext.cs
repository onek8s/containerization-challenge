using System;
using System.Collections.Generic;

namespace Contoso.Purchasing.Core
{
    public class UserContext
    {
        public UserContext()
        {
            ManagedCostCenters = new List<string>();
        }

        public string Account { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public bool IsFinance { get; set; }
        public bool IsDirector { get; set; }
        public bool IsBuyer { get; set; }
        public bool IsAdministrator { get; set; }
        public List<string> ManagedCostCenters { get; set; }

        public bool IsManager
        {
            get { return ManagedCostCenters.Count > 0; }
        }

        public bool Manages(string costCenterCode)
        {
            foreach (var code in ManagedCostCenters)
            {
                if (string.Equals(code, costCenterCode, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
