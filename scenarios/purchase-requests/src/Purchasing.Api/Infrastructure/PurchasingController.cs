using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Infrastructure
{
    public abstract class PurchasingController : ApiController
    {
        private UserContext _currentUser;

        protected PurchasingController()
        {
            Db = new Database();
            Policy = new ApprovalPolicy();
        }

        protected Database Db { get; private set; }
        protected ApprovalPolicy Policy { get; private set; }

        protected UserContext CurrentUser
        {
            get
            {
                if (_currentUser == null)
                {
                    IEnumerable<string> values;
                    var onBehalfOf = Request.Headers.TryGetValues(UserDirectory.OnBehalfOfHeader, out values) ? values.FirstOrDefault() : null;
                    _currentUser = new UserDirectory(Db).Resolve(User, onBehalfOf);
                }
                return _currentUser;
            }
        }

        protected static DateTime Now
        {
            get { return DateTime.Now; }
        }

        protected bool CanView(PurchaseRequest request)
        {
            var user = CurrentUser;
            return string.Equals(request.RequesterAccount, user.Account, StringComparison.OrdinalIgnoreCase)
                || user.Manages(request.CostCenterCode)
                || user.IsFinance || user.IsDirector || user.IsBuyer || user.IsAdministrator;
        }
    }
}
