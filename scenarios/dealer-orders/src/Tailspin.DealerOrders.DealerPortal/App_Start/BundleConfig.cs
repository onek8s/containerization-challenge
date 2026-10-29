using System.Web.Optimization;

namespace Tailspin.DealerOrders.DealerPortal
{
    public static class BundleConfig
    {
        // jQuery, Bootstrap, Knockout and the SignalR client come from the Microsoft and cdnjs CDNs (see _Layout).
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/app").Include(
                "~/Scripts/app/common.js",
                "~/Scripts/app/status.js"));
            bundles.Add(new ScriptBundle("~/bundles/configurator").Include("~/Scripts/app/configurator.js"));
            bundles.Add(new ScriptBundle("~/bundles/parts").Include("~/Scripts/app/parts.js"));
            bundles.Add(new StyleBundle("~/Content/css").Include("~/Content/site.css"));
        }
    }
}
