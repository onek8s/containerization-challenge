using System.Web;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Handlers
{
    /// <summary>GET /Cases/Attachment.ashx?id=42 streams a case attachment.</summary>
    public class AttachmentHandler : IHttpHandler, System.Web.SessionState.IReadOnlySessionState
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (!context.User.Identity.IsAuthenticated || !CareUser.IsIn(new HttpContextWrapper(context), CareRole.Agent))
            {
                context.Response.StatusCode = 403;
                return;
            }

            int id;
            if (!int.TryParse(context.Request.QueryString["id"], out id))
            {
                context.Response.StatusCode = 400;
                return;
            }

            var a = new CaseRepository().GetAttachment(id);
            if (a == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = a.ContentType;
            context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + a.FileName.Replace("\"", string.Empty) + "\"");
            context.Response.AddHeader("X-Content-Type-Options", "nosniff");
            context.Response.Cache.SetCacheability(HttpCacheability.Private);
            context.Response.BinaryWrite(a.Content);
        }
    }
}
