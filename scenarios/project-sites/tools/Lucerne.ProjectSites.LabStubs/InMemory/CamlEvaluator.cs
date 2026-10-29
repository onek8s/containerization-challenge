using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.LabStubs.InMemory
{
    /// <summary>
    /// Evaluates the CAML subset the application uses (Eq, Neq, Lt, Leq, Gt, Geq, In, IsNull, IsNotNull,
    /// BeginsWith, Contains, And, Or, OrderBy) against in-memory items.
    /// </summary>
    public static class CamlEvaluator
    {
        public static IEnumerable<ListItemData> Apply(IEnumerable<ListItemData> items, string query, string currentUserLogin)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return items;
            }

            var root = XElement.Parse("<Query>" + query + "</Query>");
            var where = root.Element("Where");
            var result = items;
            if (where != null && where.HasElements)
            {
                var condition = where.Elements().First();
                result = result.Where(i => Evaluate(condition, i, currentUserLogin));
            }

            var orderBy = root.Element("OrderBy");
            if (orderBy != null)
            {
                IOrderedEnumerable<ListItemData> ordered = null;
                foreach (var f in orderBy.Elements("FieldRef"))
                {
                    var name = (string)f.Attribute("Name");
                    var ascending = !string.Equals((string)f.Attribute("Ascending"), "FALSE", StringComparison.OrdinalIgnoreCase);
                    Func<ListItemData, IComparable> key = i => SortKey(i, name);
                    ordered = ordered == null
                        ? (ascending ? result.OrderBy(key) : result.OrderByDescending(key))
                        : (ascending ? ordered.ThenBy(key) : ordered.ThenByDescending(key));
                }

                if (ordered != null)
                {
                    result = ordered;
                }
            }

            return result;
        }

        public static bool Evaluate(XElement e, ListItemData item, string currentUser)
        {
            switch (e.Name.LocalName)
            {
                case "And":
                    return e.Elements().All(c => Evaluate(c, item, currentUser));
                case "Or":
                    return e.Elements().Any(c => Evaluate(c, item, currentUser));
                case "IsNull":
                    return IsEmpty(item[Field(e)]);
                case "IsNotNull":
                    return !IsEmpty(item[Field(e)]);
            }

            var field = Field(e);
            var actual = item[field];

            if (e.Name.LocalName == "In")
            {
                var valuesEl = e.Element("Values");
                return valuesEl != null && valuesEl.Elements("Value").Any(v => Compare(actual, v, currentUser) == 0);
            }

            var valueEl = e.Element("Value");
            if (valueEl == null)
            {
                throw new NotSupportedException("CAML element without Value: " + e);
            }

            switch (e.Name.LocalName)
            {
                case "Eq": return !IsEmpty(actual) && Compare(actual, valueEl, currentUser) == 0 || (IsEmpty(actual) && IsFalseBoolean(valueEl));
                case "Neq": return IsEmpty(actual) || Compare(actual, valueEl, currentUser) != 0;
                case "Lt": return !IsEmpty(actual) && Compare(actual, valueEl, currentUser) < 0;
                case "Leq": return !IsEmpty(actual) && Compare(actual, valueEl, currentUser) <= 0;
                case "Gt": return !IsEmpty(actual) && Compare(actual, valueEl, currentUser) > 0;
                case "Geq": return !IsEmpty(actual) && Compare(actual, valueEl, currentUser) >= 0;
                case "BeginsWith": return Text(actual).StartsWith(valueEl.Value, StringComparison.OrdinalIgnoreCase);
                case "Contains": return Text(actual).IndexOf(valueEl.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                default: throw new NotSupportedException("CAML operator " + e.Name.LocalName);
            }
        }

        private static bool IsFalseBoolean(XElement value)
        {
            // SharePoint treats an empty Yes/No field as No.
            return (string)value.Attribute("Type") == "Boolean" && value.Value == "0";
        }

        private static string Field(XElement e)
        {
            return (string)e.Element("FieldRef").Attribute("Name");
        }

        private static bool IsEmpty(object v)
        {
            return v == null || (v is string s && s.Length == 0) || (v is string[] a && a.Length == 0);
        }

        private static string Text(object v)
        {
            switch (v)
            {
                case null: return string.Empty;
                case UserValue u: return u.Login ?? string.Empty;
                case LookupValue l: return l.Value ?? string.Empty;
                case string[] a: return string.Join(";", a);
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        private static int Compare(object actual, XElement value, string currentUser)
        {
            var type = (string)value.Attribute("Type") ?? "Text";
            var text = value.Value;
            if (value.Element("UserID") != null)
            {
                text = currentUser;
                type = "User";
            }

            switch (type)
            {
                case "Boolean":
                    var b = actual is bool x ? x : Text(actual) == "1" || string.Equals(Text(actual), "true", StringComparison.OrdinalIgnoreCase);
                    return (b ? "1" : "0") == text ? 0 : 1;
                case "DateTime":
                    var d = actual is DateTime dt ? dt : DateTime.Parse(Text(actual), CultureInfo.InvariantCulture);
                    var includeTime = string.Equals((string)value.Attribute("IncludeTimeValue"), "TRUE", StringComparison.OrdinalIgnoreCase);
                    var other = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                    return includeTime ? d.ToUniversalTime().CompareTo(other) : d.Date.CompareTo(other.Date);
                case "Number":
                case "Integer":
                case "Counter":
                    return Convert.ToDecimal(actual is string ? decimal.Parse((string)actual, CultureInfo.InvariantCulture) : actual, CultureInfo.InvariantCulture)
                        .CompareTo(decimal.Parse(text, CultureInfo.InvariantCulture));
                case "User":
                    return string.Equals(Text(actual), text, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                case "MultiChoice":
                    return actual is string[] arr && arr.Contains(text, StringComparer.OrdinalIgnoreCase) ? 0 : 1;
                default:
                    return string.Compare(Text(actual), text, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static IComparable SortKey(ListItemData item, string field)
        {
            var v = item[field];
            if (field == "ID")
            {
                return item.Id;
            }

            return v switch
            {
                null => string.Empty,
                DateTime d => d.ToString("o"),
                double n => n.ToString("000000000000.0000", CultureInfo.InvariantCulture),
                decimal m => m.ToString("000000000000.0000", CultureInfo.InvariantCulture),
                int i => i.ToString("0000000000", CultureInfo.InvariantCulture),
                _ => Text(v)
            };
        }
    }
}
