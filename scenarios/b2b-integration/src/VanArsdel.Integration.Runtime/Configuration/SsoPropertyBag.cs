using System;
using System.Collections.Generic;
using Microsoft.EnterpriseSingleSignOn.Interop;

namespace VanArsdel.Integration.Runtime.Configuration
{
    /// <summary>IPropertyBag the SSO config store fills with the fields of a config application.</summary>
    public class SsoPropertyBag : IPropertyBag
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public void Read(string propName, out object ptrVar, int errorLog)
        {
            _values.TryGetValue(propName, out ptrVar);
        }

        public void Write(string propName, ref object ptrVar)
        {
            _values[propName] = ptrVar;
        }

        public IDictionary<string, object> Values
        {
            get { return _values; }
        }
    }
}
