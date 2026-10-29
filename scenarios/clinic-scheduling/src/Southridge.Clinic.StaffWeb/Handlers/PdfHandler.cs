using System;
using System.Globalization;
using System.Web;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb.Handlers
{
    /// <summary>Base for the handlers that stream generated documents. Requires a signed-in user.</summary>
    public abstract class PdfHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (!context.User.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = 401;
                return;
            }

            int id;
            if (!int.TryParse(context.Request.QueryString["id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                context.Response.StatusCode = 400;
                return;
            }

            try
            {
                string fileName;
                string contentType = "application/pdf";
                byte[] content = Create(context, id, out fileName, ref contentType);
                if (content == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.ContentType = contentType;
                context.Response.AddHeader("Content-Disposition", "inline; filename=" + fileName);
                context.Response.Cache.SetCacheability(HttpCacheability.Private);
                context.Response.BinaryWrite(content);
            }
            catch (Exception ex)
            {
                Exception toThrow = ErrorHandler.Handle(ex, ExceptionPolicies.UserInterface);
                if (toThrow != null)
                {
                    throw new HttpException(500, "Document could not be created.", toThrow);
                }
            }
        }

        protected abstract byte[] Create(HttpContext context, int id, out string fileName, ref string contentType);
    }
}
