using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lucerne.ProjectSites.SharePoint
{
    /// <summary>Person field value. Login is the claims encoded account (i:0#.w|corp\name).</summary>
    public sealed class UserValue
    {
        public UserValue(string login, string displayName = null, int id = 0)
        {
            Login = login;
            DisplayName = displayName;
            Id = id;
        }

        public string Login { get; }
        public string DisplayName { get; }
        public int Id { get; }

        public override string ToString()
        {
            return Login;
        }
    }

    public sealed class LookupValue
    {
        public LookupValue(int id, string value)
        {
            Id = id;
            Value = value;
        }

        public int Id { get; }
        public string Value { get; }
    }

    public sealed class UrlValue
    {
        public UrlValue(string url, string description)
        {
            Url = url;
            Description = description;
        }

        public string Url { get; }
        public string Description { get; }
    }

    public class FieldValues : Dictionary<string, object>
    {
        public FieldValues()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public FieldValues(IDictionary<string, object> values)
            : base(values, StringComparer.OrdinalIgnoreCase)
        {
        }

        public FieldValues Set(string field, object value)
        {
            this[field] = value;
            return this;
        }
    }

    public class ListItemData
    {
        public ListItemData(int id, IDictionary<string, object> values)
        {
            Id = id;
            Values = new FieldValues(values);
        }

        public int Id { get; }
        public FieldValues Values { get; }

        public object this[string field]
        {
            get
            {
                object v;
                return Values.TryGetValue(field, out v) ? v : null;
            }
        }

        public string GetString(string field)
        {
            var v = this[field];
            if (v == null)
            {
                return null;
            }

            var u = v as UserValue;
            if (u != null)
            {
                return u.Login;
            }

            var l = v as LookupValue;
            if (l != null)
            {
                return l.Value;
            }

            var url = v as UrlValue;
            if (url != null)
            {
                return url.Url;
            }

            var arr = v as string[];
            if (arr != null)
            {
                return string.Join(";", arr);
            }

            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public DateTime? GetDate(string field)
        {
            var v = this[field];
            if (v == null)
            {
                return null;
            }

            if (v is DateTime)
            {
                return (DateTime)v;
            }

            DateTime d;
            return DateTime.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out d) ? d : (DateTime?)null;
        }

        public decimal GetDecimal(string field)
        {
            var v = this[field];
            if (v == null)
            {
                return 0m;
            }

            decimal d;
            return decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : 0m;
        }

        public int GetInt(string field)
        {
            return (int)GetDecimal(field);
        }

        public bool GetBool(string field)
        {
            var v = this[field];
            if (v == null)
            {
                return false;
            }

            if (v is bool)
            {
                return (bool)v;
            }

            var s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase);
        }

        public string[] GetMulti(string field)
        {
            var v = this[field];
            var arr = v as string[];
            if (arr != null)
            {
                return arr;
            }

            var s = GetString(field);
            return string.IsNullOrEmpty(s) ? new string[0] : s.Split(new[] { ";#", ";" }, StringSplitOptions.RemoveEmptyEntries);
        }

        public string GetUserLogin(string field)
        {
            var u = this[field] as UserValue;
            return u != null ? u.Login : GetString(field);
        }

        public TEnum GetEnum<TEnum>(string field, TEnum fallback)
            where TEnum : struct
        {
            TEnum value;
            var s = GetString(field);
            return s != null && Enum.TryParse(s.Replace(" ", string.Empty), true, out value) ? value : fallback;
        }
    }
}
