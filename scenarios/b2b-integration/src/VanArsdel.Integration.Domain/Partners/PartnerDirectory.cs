using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Partners
{
    /// <summary>
    /// In-memory view of the partner tables, loaded once per host instance and refreshed on a timer. Resolves the
    /// sender of an interchange, an AS2 message or a gateway call to a PartnerId.
    /// </summary>
    public class PartnerDirectory
    {
        private readonly Dictionary<string, TradingPartner> _byId;
        private readonly Dictionary<string, PartnerCrossReference> _xref;

        public PartnerDirectory(IEnumerable<TradingPartner> partners, IEnumerable<PartnerCrossReference> crossReferences)
        {
            _byId = new Dictionary<string, TradingPartner>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in partners ?? Enumerable.Empty<TradingPartner>())
            {
                _byId[p.PartnerId] = p;
            }

            _xref = new Dictionary<string, PartnerCrossReference>(StringComparer.Ordinal);
            foreach (var x in crossReferences ?? Enumerable.Empty<PartnerCrossReference>())
            {
                _xref[PartnerCrossReference.Key(x.PartnerId, x.Kind, x.PartnerValue)] = x;
            }

            LoadedUtc = DateTime.UtcNow;
        }

        public DateTime LoadedUtc { get; private set; }

        public IEnumerable<TradingPartner> All
        {
            get { return _byId.Values.OrderBy(p => p.PartnerId); }
        }

        public TradingPartner Find(string partnerId)
        {
            TradingPartner partner;
            return partnerId != null && _byId.TryGetValue(partnerId.Trim(), out partner) ? partner : null;
        }

        public TradingPartner Get(string partnerId)
        {
            var partner = Find(partnerId);
            if (partner == null)
            {
                throw new UnknownPartnerException("Unknown trading partner '" + partnerId + "'.");
            }

            return partner;
        }

        public TradingPartner FindByInterchange(string qualifier, string id)
        {
            return _byId.Values.FirstOrDefault(p => p.Active && p.MatchesInterchange(qualifier, id));
        }

        public TradingPartner FindByAs2(string as2From)
        {
            if (string.IsNullOrWhiteSpace(as2From))
            {
                return null;
            }

            var id = as2From.Trim().Trim('"');
            return _byId.Values.FirstOrDefault(p => p.Active && string.Equals(p.As2Identifier, id, StringComparison.OrdinalIgnoreCase));
        }

        public TradingPartner FindByGln(string gln)
        {
            return string.IsNullOrWhiteSpace(gln) ? null : _byId.Values.FirstOrDefault(p => p.Gln == gln.Trim());
        }

        public TradingPartner FindByErpNumber(PartnerRole role, string erpNumber)
        {
            if (string.IsNullOrWhiteSpace(erpNumber))
            {
                return null;
            }

            var trimmed = erpNumber.Trim().TrimStart('0');
            return _byId.Values.FirstOrDefault(p => p.Role == role && (p.ErpNumber ?? string.Empty).TrimStart('0') == trimmed);
        }

        /// <summary>Returns the internal value, or null when the partner has no cross-reference for it.</summary>
        public string Translate(string partnerId, CrossReferenceKind kind, string partnerValue, DateTime onDate)
        {
            PartnerCrossReference x;
            if (_xref.TryGetValue(PartnerCrossReference.Key(partnerId, kind, partnerValue), out x) && x.IsValidOn(onDate))
            {
                return x.InternalValue;
            }

            return null;
        }

        /// <summary>Internal value back to the partner's value (used on outbound documents).</summary>
        public string TranslateBack(string partnerId, CrossReferenceKind kind, string internalValue, DateTime onDate)
        {
            var match = _xref.Values.FirstOrDefault(x =>
                string.Equals(x.PartnerId, partnerId, StringComparison.OrdinalIgnoreCase)
                && x.Kind == kind
                && string.Equals(x.InternalValue, internalValue, StringComparison.OrdinalIgnoreCase)
                && x.IsValidOn(onDate));
            return match == null ? null : match.PartnerValue;
        }
    }

    [Serializable]
    public class UnknownPartnerException : Exception
    {
        public UnknownPartnerException(string message) : base(message)
        {
        }

        protected UnknownPartnerException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
