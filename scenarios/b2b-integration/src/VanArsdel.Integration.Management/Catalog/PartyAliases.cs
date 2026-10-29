using System;
using System.Collections.Generic;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Management.Catalog
{
    /// <summary>
    /// The aliases the BizTalk party of a trading partner must carry. The receive pipelines resolve partners from
    /// dbo.TradingPartner, but the EDI and AS2 runtime resolve agreements by party alias, so both have to agree.
    /// </summary>
    public static class PartyAliases
    {
        public const string As2Alias = "AS2-From";
        public const string X12Alias = "X12";
        public const string EdifactAlias = "EDIFACT";
        public const string GlnAlias = "GLN";
        public const string OrganizationAlias = "Organization";

        public static IList<PartyAliasSummary> Expected(TradingPartner partner)
        {
            if (partner == null)
            {
                throw new ArgumentNullException("partner");
            }

            var list = new List<PartyAliasSummary>
            {
                new PartyAliasSummary { Name = OrganizationAlias, Qualifier = "OrganizationName", Value = partner.PartnerId }
            };

            if (!string.IsNullOrEmpty(partner.As2Identifier))
            {
                list.Add(new PartyAliasSummary { Name = As2Alias, Qualifier = "AS2Identifier", Value = partner.As2Identifier });
            }

            if (!string.IsNullOrEmpty(partner.InterchangeId))
            {
                if (partner.Standard == DocumentStandard.X12)
                {
                    list.Add(new PartyAliasSummary { Name = X12Alias, Qualifier = partner.InterchangeQualifier, Value = partner.InterchangeId });
                }
                else if (partner.Standard == DocumentStandard.Edifact)
                {
                    list.Add(new PartyAliasSummary { Name = EdifactAlias, Qualifier = partner.InterchangeQualifier, Value = partner.InterchangeId });
                }
            }

            if (!string.IsNullOrEmpty(partner.Gln))
            {
                list.Add(new PartyAliasSummary { Name = GlnAlias, Qualifier = "14", Value = partner.Gln });
            }

            return list;
        }

        public static bool Same(PartyAliasSummary a, PartyAliasSummary b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Qualifier ?? string.Empty, b.Qualifier ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Value ?? string.Empty, b.Value ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Aliases to add and to remove so the party matches the partner record. Only the aliases this class manages
        /// are removed; the Organization alias BizTalk creates with the party and any hand-made alias stay.
        /// </summary>
        public static void Diff(IList<PartyAliasSummary> current, IList<PartyAliasSummary> expected, IList<PartyAliasSummary> toAdd, IList<PartyAliasSummary> toRemove)
        {
            foreach (var e in expected)
            {
                if (!Contains(current, e))
                {
                    toAdd.Add(e);
                }
            }

            foreach (var c in current)
            {
                if (IsManaged(c.Name) && !Contains(expected, c))
                {
                    toRemove.Add(c);
                }
            }
        }

        private static bool Contains(IEnumerable<PartyAliasSummary> list, PartyAliasSummary alias)
        {
            foreach (var item in list)
            {
                if (Same(item, alias))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsManaged(string aliasName)
        {
            return aliasName == As2Alias || aliasName == X12Alias || aliasName == EdifactAlias || aliasName == GlnAlias;
        }
    }
}
