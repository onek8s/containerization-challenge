using System;
using System.Security.Claims;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Tailspin.DealerOrders.Data;

namespace Tailspin.DealerOrders.DealerPortal.Infrastructure
{
    /// <summary>Base for signed-in pages. Every query is limited to the dealer in the user's claims.</summary>
    public abstract class PortalController : Controller
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatString = "yyyy-MM-dd",
            Converters = { new StringEnumConverter() }
        };

        protected int DealerId
        {
            get
            {
                var claim = (User.Identity as ClaimsIdentity)?.FindFirst(PortalUser.DealerIdClaim);
                if (claim == null) throw new InvalidOperationException("The signed-in user has no dealer.");
                return int.Parse(claim.Value);
            }
        }

        protected string UserName => User.Identity.Name;

        protected string DisplayName => (User.Identity as ClaimsIdentity)?.FindFirst(PortalUser.DisplayNameClaim)?.Value ?? UserName;

        protected ContentResult JsonNet(object data) => Content(JsonConvert.SerializeObject(data, JsonSettings), "application/json");

        protected T FromJsonBody<T>()
        {
            Request.InputStream.Position = 0;
            using (var reader = new System.IO.StreamReader(Request.InputStream))
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd(), JsonSettings);
        }

        protected void Flash(string message) => TempData["Message"] = message;

        protected void FlashError(string message) => TempData["Error"] = message;

        protected ActionResult BackOr(string action, object routeValues = null)
        {
            var referrer = Request.UrlReferrer;
            if (referrer != null && Url.IsLocalUrl(referrer.PathAndQuery)) return Redirect(referrer.PathAndQuery);
            return RedirectToAction(action, routeValues);
        }
    }
}
