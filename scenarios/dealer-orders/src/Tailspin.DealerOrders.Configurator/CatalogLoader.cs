using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Configurator
{
    /// <summary>
    /// Reads machine model definitions (options and rules) from the JSON files maintained by product management.
    /// One file per model and model year, e.g. <c>TF14-2019.json</c>.
    /// </summary>
    public static class CatalogLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Error,
            DateFormatString = "yyyy-MM-dd"
        };

        public static ProductModel Parse(string json)
        {
            var model = JsonConvert.DeserializeObject<ProductModel>(json, Settings);
            Validate(model);
            return model;
        }

        public static string Serialize(ProductModel model) => JsonConvert.SerializeObject(model, Formatting.Indented, Settings);

        public static IList<ProductModel> LoadFolder(string folder)
        {
            var models = new List<ProductModel>();
            foreach (var file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    models.Add(Parse(File.ReadAllText(file)));
                }
                catch (Exception ex) when (ex is JsonException || ex is InvalidDataException)
                {
                    throw new InvalidDataException($"Catalog file {Path.GetFileName(file)}: {ex.Message}", ex);
                }
            }
            return models;
        }

        private static void Validate(ProductModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ModelCode)) throw new InvalidDataException("modelCode is missing.");
            if (string.IsNullOrWhiteSpace(model.ItemId)) throw new InvalidDataException($"{model.ModelCode}: itemId is missing.");
            var codes = model.AllOptions.Select(o => o.Code).ToList();
            var duplicates = codes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0) throw new InvalidDataException($"{model.ModelCode}: duplicate option codes {string.Join(", ", duplicates)}.");
            foreach (var rule in model.Rules)
            {
                if (string.IsNullOrEmpty(rule.Id)) throw new InvalidDataException($"{model.ModelCode}: a rule has no id.");
                if (rule.Kind == OptionRuleKind.Condition && string.IsNullOrWhiteSpace(rule.Expression))
                    throw new InvalidDataException($"{model.ModelCode}: rule {rule.Id} has no expression.");
                if (rule.Kind != OptionRuleKind.Condition && (rule.Targets == null || rule.Targets.Count == 0))
                    throw new InvalidDataException($"{model.ModelCode}: rule {rule.Id} has no targets.");
            }
        }
    }
}
