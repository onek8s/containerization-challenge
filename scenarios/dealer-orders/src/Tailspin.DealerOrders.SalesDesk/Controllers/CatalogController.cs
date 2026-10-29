using System;
using System.Linq;
using System.Web.Mvc;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    /// <summary>Read-only view of the machine catalog and its option rules (maintained as JSON by product management).</summary>
    public class CatalogController : DeskController
    {
        private readonly ICatalog _catalog;
        private readonly ConfigurationEngine _engine;

        public CatalogController(ICatalog catalog, ConfigurationEngine engine)
        {
            _catalog = catalog;
            _engine = engine;
        }

        public ActionResult Index() => View(_catalog.All().OrderBy(m => m.Family).ThenBy(m => m.ModelCode).ToList());

        public ActionResult Model(string id)
        {
            var model = _catalog.Get(id);
            if (model == null) return HttpNotFound();
            // Compiling shows rule syntax errors to the desk before a dealer meets them.
            try
            {
                _engine.CompileRules(model);
                ViewBag.RuleError = null;
            }
            catch (Exception ex)
            {
                ViewBag.RuleError = ex.Message;
            }
            return View(model);
        }
    }
}
