using System.IdentityModel.Selectors;
using System.ServiceModel;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;

namespace Northwind.Inspections.Service
{
    public class PartnerPasswordValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            if (!new AdjusterRepository(ServiceRuntime.Db).VerifyPartner(userName, password))
            {
                InspectionsLog.Warning(EventId.PartnerLoginFailed, "Partner sign-in failed for '{0}'.", userName);
                throw new FaultException("Unknown user name or incorrect password.");
            }
        }
    }
}
