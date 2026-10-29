using System;
using System.Web;
using System.Web.UI;
using Tailspin.CustomerCare.Data.Services;

namespace Tailspin.CustomerCare.Web.Security
{
    /// <summary>Base class for the Web Forms pages: role check, current user and user messages.</summary>
    public class CarePage : Page
    {
        protected virtual CareRole RequiredRole
        {
            get { return CareRole.Agent; }
        }

        protected CareUserProfile CurrentUser
        {
            get { return CareUser.Get(new HttpContextWrapper(Context)); }
        }

        protected string AccountName
        {
            get { return User.Identity.Name; }
        }

        protected override void OnInit(EventArgs e)
        {
            if (!User.Identity.IsAuthenticated || !CareUser.IsIn(new HttpContextWrapper(Context), RequiredRole))
            {
                throw new HttpException(403, "You do not have access to this page.");
            }

            // ViewState is tied to the signed-in user.
            ViewStateUserKey = User.Identity.Name;
            base.OnInit(e);
        }

        protected void ShowMessage(string text, bool isError = false)
        {
            var master = Master as SiteMaster;
            if (master != null) master.ShowMessage(text, isError);
        }

        /// <summary>Runs a page action and turns the expected errors into a message instead of the error page.</summary>
        protected bool Try(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (ConcurrencyConflictException ex)
            {
                ShowMessage(ex.Message, true);
            }
            catch (ArgumentException ex)
            {
                ShowMessage(ex.Message, true);
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message, true);
            }

            return false;
        }

        protected int? QueryInt(string name)
        {
            var raw = RouteData.Values[name] as string ?? Request.QueryString[name];
            int v;
            return int.TryParse(raw, out v) ? v : (int?)null;
        }
    }
}
