using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Catalog
{
    /// <summary>
    /// Machine models from the catalog folder (models\*.json, maintained by product management and deployed with the
    /// applications) and dealer-orderable parts from parts.json or the item cache.
    /// </summary>
    public class FileCatalog : ICatalog
    {
        private readonly Dictionary<string, ProductModel> _models;
        private readonly Dictionary<string, string> _parts;
        private readonly Func<string, string> _partLookup;

        public FileCatalog(string folder, Func<string, string> partLookup = null)
            : this(CatalogLoader.LoadFolder(Path.Combine(folder, "models")), LoadParts(Path.Combine(folder, "parts.json")), partLookup)
        {
        }

        public FileCatalog(IEnumerable<ProductModel> models, IDictionary<string, string> parts, Func<string, string> partLookup = null)
        {
            // When several model years are present the newest one wins.
            _models = models.GroupBy(m => m.ModelCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.ModelYear).First(), StringComparer.OrdinalIgnoreCase);
            _parts = new Dictionary<string, string>(parts ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _partLookup = partLookup;
        }

        public ProductModel Get(string modelCode) =>
            modelCode != null && _models.TryGetValue(modelCode, out var model) ? model : null;

        public ProductModel FindByItemId(string itemId) =>
            _models.Values.FirstOrDefault(m => string.Equals(m.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

        public IList<ProductModel> All() => _models.Values.OrderBy(m => m.Family).ThenBy(m => m.ModelCode).ToList();

        public string PartDescription(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_parts.TryGetValue(itemId, out var name)) return name;
            return _partLookup?.Invoke(itemId);
        }

        public IDictionary<string, string> Parts => _parts;

        public static IDictionary<string, string> LoadParts(string file)
        {
            if (!File.Exists(file)) return new Dictionary<string, string>();
            var items = JsonConvert.DeserializeObject<List<PartItem>>(File.ReadAllText(file));
            return items.ToDictionary(i => i.ItemId, i => i.Name, StringComparer.OrdinalIgnoreCase);
        }

        private class PartItem
        {
            public string ItemId { get; set; }
            public string Name { get; set; }
        }
    }
}
