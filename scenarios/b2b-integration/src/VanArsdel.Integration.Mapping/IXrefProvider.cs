using System;
using System.Configuration;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Mapping
{
    /// <summary>Partner data the maps need. Implemented over dbo.PartnerXref (Runtime) or a PartnerDirectory.</summary>
    public interface IXrefProvider
    {
        TradingPartner Partner(string partnerId);
        TradingPartner PartnerByInterchange(string qualifier, string id);
        TradingPartner PartnerByErpNumber(PartnerRole role, string erpNumber);
        string ToInternal(string partnerId, CrossReferenceKind kind, string partnerValue);
        string ToPartner(string partnerId, CrossReferenceKind kind, string internalValue);
    }

    public class DirectoryXrefProvider : IXrefProvider
    {
        private readonly PartnerDirectory _directory;
        private readonly Func<DateTime> _today;

        public DirectoryXrefProvider(PartnerDirectory directory, Func<DateTime> today = null)
        {
            _directory = directory;
            _today = today ?? (() => DateTime.Today);
        }

        public TradingPartner Partner(string partnerId)
        {
            return _directory.Find(partnerId);
        }

        public TradingPartner PartnerByInterchange(string qualifier, string id)
        {
            return _directory.FindByInterchange(qualifier, id);
        }

        public TradingPartner PartnerByErpNumber(PartnerRole role, string erpNumber)
        {
            return _directory.FindByErpNumber(role, erpNumber);
        }

        public string ToInternal(string partnerId, CrossReferenceKind kind, string partnerValue)
        {
            return _directory.Translate(partnerId, kind, partnerValue, _today());
        }

        public string ToPartner(string partnerId, CrossReferenceKind kind, string internalValue)
        {
            return _directory.TranslateBack(partnerId, kind, internalValue, _today());
        }
    }

    /// <summary>
    /// Where the scripting functoids get their IXrefProvider. BizTalk creates the functoid classes itself, so the
    /// provider is static: the host sets it at start-up, or it is created from the appSettings key
    /// VanArsdel.XrefProvider (assembly qualified type name) in BTSNTSvc64.exe.config.
    /// </summary>
    public static class XrefProviderLocator
    {
        public const string ConfigKey = "VanArsdel.XrefProvider";
        private static readonly object Gate = new object();
        private static IXrefProvider _current;

        public static IXrefProvider Current
        {
            get
            {
                if (_current != null)
                {
                    return _current;
                }

                lock (Gate)
                {
                    if (_current == null)
                    {
                        _current = CreateFromConfiguration();
                    }

                    return _current;
                }
            }

            set
            {
                lock (Gate)
                {
                    _current = value;
                }
            }
        }

        private static IXrefProvider CreateFromConfiguration()
        {
            var typeName = ConfigurationManager.AppSettings[ConfigKey];
            if (string.IsNullOrEmpty(typeName))
            {
                throw new InvalidOperationException("No cross-reference provider configured (appSettings " + ConfigKey + ").");
            }

            var type = Type.GetType(typeName, true);
            return (IXrefProvider)Activator.CreateInstance(type);
        }
    }
}
