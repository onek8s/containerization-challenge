using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>A machine model with the selected options.</summary>
    public class MachineConfiguration
    {
        public MachineConfiguration()
        {
            Options = new List<string>();
        }

        public MachineConfiguration(string modelCode, IEnumerable<string> options)
        {
            ModelCode = modelCode;
            Options = options.Select(o => o.Trim().ToUpperInvariant()).Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
        }

        public string ModelCode { get; set; }
        public List<string> Options { get; set; }

        public bool Has(string option) => Options.Contains(option, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// AX configuration id (ConfigId, 10 characters). The same model and option set always gives the same id so
        /// that repeat orders reuse the configuration that already exists in AX.
        /// </summary>
        public string ConfigurationId
        {
            get
            {
                var key = ModelCode.ToUpperInvariant() + "|" + string.Join(",", Options.OrderBy(o => o, StringComparer.Ordinal));
                using (var sha = SHA1.Create())
                {
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                    var prefix = new string(ModelCode.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
                    var sb = new StringBuilder(prefix);
                    const string alphabet = "0123456789ABCDEFGHJKLMNPRSTUVWXY";
                    var bits = BitConverter.ToUInt64(hash, 0);
                    while (sb.Length < 10)
                    {
                        sb.Append(alphabet[(int)(bits & 31)]);
                        bits >>= 5;
                    }
                    return sb.ToString();
                }
            }
        }

        public string Describe() => ModelCode + (Options.Count == 0 ? "" : " + " + string.Join(", ", Options));
    }
}
