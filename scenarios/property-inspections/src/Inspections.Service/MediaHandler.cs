using System;
using System.IO;
using System.Web;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;

namespace Northwind.Inspections.Service
{
    /// <summary>
    /// GET /media/{photo id}[/thumb]. Reached through the portal (inspections.corp.local/media/...), which forwards the
    /// request with the portal's forms authentication cookie; both sites share the machine key.
    /// </summary>
    public class MediaHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (context.User == null || !context.User.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = 401;
                return;
            }

            // Request.Path keeps the "/thumb" suffix that IIS may split off into PathInfo for this handler mapping.
            var parts = VirtualPathUtility.ToAppRelative(context.Request.Path).Substring(2).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Guid id;
            if (parts.Length < 2 || !Guid.TryParse(parts[1], out id))
            {
                context.Response.StatusCode = 404;
                return;
            }
            var size = parts.Length > 2 && parts[2].Equals("thumb", StringComparison.OrdinalIgnoreCase) ? PhotoSize.Thumbnail : PhotoSize.Full;

            var photo = new PhotoRepository(ServiceRuntime.Db).Get(id);
            if (photo == null || photo.Purged)
            {
                context.Response.StatusCode = 404;
                return;
            }

            var etag = "\"" + photo.Id.ToString("N") + (size == PhotoSize.Thumbnail ? "t" : "f") + "\"";
            if (context.Request.Headers["If-None-Match"] == etag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            byte[] bytes;
            try
            {
                bytes = ServiceRuntime.Store.Read(photo, size);
            }
            catch (IOException ex)
            {
                InspectionsLog.Error(EventId.ShareFailure, ex, "Reading photo {0} from the share failed.", photo.Id);
                context.Response.StatusCode = 503;
                return;
            }

            context.Response.ContentType = "image/jpeg";
            context.Response.Cache.SetCacheability(HttpCacheability.Private);
            context.Response.Cache.SetMaxAge(TimeSpan.FromDays(7));
            context.Response.Cache.SetETag(etag);
            if (size == PhotoSize.Full && context.Request.QueryString["download"] == "1")
            {
                context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + Path.GetFileNameWithoutExtension(photo.OriginalFileName) + ".jpg\"");
            }
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
