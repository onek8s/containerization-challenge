using System;
using System.Globalization;
using System.Linq;
using System.Security;

namespace Lucerne.ProjectSites.SharePoint
{
    /// <summary>Small CAML builder used instead of hand-written query strings (fewer typos in field names).</summary>
    public static class Caml
    {
        public static string Where(string condition)
        {
            return string.IsNullOrEmpty(condition) ? string.Empty : "<Where>" + condition + "</Where>";
        }

        public static string OrderBy(string field, bool ascending = true)
        {
            return "<OrderBy><FieldRef Name='" + field + "' Ascending='" + (ascending ? "TRUE" : "FALSE") + "' /></OrderBy>";
        }

        public static string Eq(string field, string type, object value)
        {
            return Compare("Eq", field, type, value);
        }

        public static string Neq(string field, string type, object value)
        {
            return Compare("Neq", field, type, value);
        }

        public static string Lt(string field, string type, object value)
        {
            return Compare("Lt", field, type, value);
        }

        public static string Leq(string field, string type, object value)
        {
            return Compare("Leq", field, type, value);
        }

        public static string Gt(string field, string type, object value)
        {
            return Compare("Gt", field, type, value);
        }

        public static string Geq(string field, string type, object value)
        {
            return Compare("Geq", field, type, value);
        }

        public static string BeginsWith(string field, string value)
        {
            return Compare("BeginsWith", field, "Text", value);
        }

        public static string Contains(string field, string value)
        {
            return Compare("Contains", field, "Text", value);
        }

        public static string IsNull(string field)
        {
            return "<IsNull><FieldRef Name='" + field + "' /></IsNull>";
        }

        public static string IsNotNull(string field)
        {
            return "<IsNotNull><FieldRef Name='" + field + "' /></IsNotNull>";
        }

        public static string In(string field, string type, params object[] values)
        {
            return "<In><FieldRef Name='" + field + "' /><Values>"
                + string.Concat(values.Select(v => Value(type, v))) + "</Values></In>";
        }

        /// <summary>Person field compared by account (LookupId='FALSE'), e.g. i:0#.w|corp\name.</summary>
        public static string EqUser(string field, string login)
        {
            return "<Eq><FieldRef Name='" + field + "' /><Value Type='User'>" + SecurityElement.Escape(login) + "</Value></Eq>";
        }

        public static string EqCurrentUser(string field)
        {
            return "<Eq><FieldRef Name='" + field + "' /><Value Type='Integer'><UserID /></Value></Eq>";
        }

        /// <summary>CAML And/Or are binary; more than two operands are nested to the right.</summary>
        public static string And(params string[] conditions)
        {
            return Combine("And", conditions);
        }

        public static string Or(params string[] conditions)
        {
            return Combine("Or", conditions);
        }

        private static string Combine(string op, string[] conditions)
        {
            var parts = conditions.Where(c => !string.IsNullOrEmpty(c)).ToArray();
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            var result = "<" + op + ">" + parts[parts.Length - 2] + parts[parts.Length - 1] + "</" + op + ">";
            for (var i = parts.Length - 3; i >= 0; i--)
            {
                result = "<" + op + ">" + parts[i] + result + "</" + op + ">";
            }

            return result;
        }

        private static string Compare(string op, string field, string type, object value)
        {
            return "<" + op + "><FieldRef Name='" + field + "' />" + Value(type, value) + "</" + op + ">";
        }

        private static string Value(string type, object value)
        {
            string text;
            if (value is DateTime)
            {
                text = ((DateTime)value).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                return "<Value Type='DateTime' IncludeTimeValue='TRUE'>" + text + "</Value>";
            }

            if (value is bool)
            {
                text = (bool)value ? "1" : "0";
            }
            else
            {
                text = Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return "<Value Type='" + type + "'>" + SecurityElement.Escape(text) + "</Value>";
        }
    }
}
