using System;
using System.Collections.Generic;
using System.Linq;

namespace ProductReturns.Core
{
    public sealed class ReturnReason
    {
        public ReturnReason(string code, string text, bool companyFault)
        {
            Code = code;
            Text = text;
            CompanyFault = companyFault;
        }

        public string Code { get; }

        public string Text { get; }

        /// <summary>The return is caused by us or the carrier: free label, no restocking fee.</summary>
        public bool CompanyFault { get; }
    }

    public static class ReturnReasons
    {
        public static readonly IReadOnlyList<ReturnReason> All = new[]
        {
            new ReturnReason("DEFECTIVE", "Item is defective or does not work", true),
            new ReturnReason("DAMAGED", "Item arrived damaged", true),
            new ReturnReason("WRONG_ITEM", "Wrong item was delivered", true),
            new ReturnReason("NOT_AS_DESCRIBED", "Item is not as described", true),
            new ReturnReason("NO_LONGER_NEEDED", "No longer needed", false),
            new ReturnReason("CHANGED_MIND", "Changed my mind", false),
            new ReturnReason("BETTER_PRICE", "Found a better price", false)
        };

        public static ReturnReason Find(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            return All.FirstOrDefault(r => string.Equals(r.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsCompanyFault(string code)
        {
            var reason = Find(code);
            return reason != null && reason.CompanyFault;
        }
    }
}
