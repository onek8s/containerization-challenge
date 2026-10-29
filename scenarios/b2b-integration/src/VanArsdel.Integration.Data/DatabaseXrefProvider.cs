using System;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Mapping;

namespace VanArsdel.Integration.Data
{
    /// <summary>
    /// IXrefProvider for the BizTalk hosts: the partner directory is cached for five minutes per host instance so the
    /// maps do not hit SQL for every functoid call. Configured in BTSNTSvc64.exe.config:
    /// &lt;add key="VanArsdel.XrefProvider" value="VanArsdel.Integration.Data.DatabaseXrefProvider, VanArsdel.Integration.Data, ..." /&gt;
    /// </summary>
    public class DatabaseXrefProvider : IXrefProvider
    {
        private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(5);
        private static readonly object Gate = new object();
        private static PartnerDirectory _cached;
        private static DateTime _loadedUtc;

        private readonly PartnerRepository _repository;

        public DatabaseXrefProvider() : this(new PartnerRepository(new IntegrationDatabase()))
        {
        }

        public DatabaseXrefProvider(PartnerRepository repository)
        {
            _repository = repository;
        }

        private PartnerDirectory Directory
        {
            get
            {
                lock (Gate)
                {
                    if (_cached == null || DateTime.UtcNow - _loadedUtc > CacheTime)
                    {
                        _cached = _repository.LoadDirectory();
                        _loadedUtc = DateTime.UtcNow;
                    }

                    return _cached;
                }
            }
        }

        /// <summary>The cached directory, for components that resolve partners outside the maps (pipelines, orchestrations).</summary>
        public PartnerDirectory CurrentDirectory
        {
            get { return Directory; }
        }

        public static void Invalidate()
        {
            lock (Gate)
            {
                _cached = null;
            }
        }

        public TradingPartner Partner(string partnerId)
        {
            return Directory.Find(partnerId);
        }

        public TradingPartner PartnerByInterchange(string qualifier, string id)
        {
            return Directory.FindByInterchange(qualifier, id);
        }

        public TradingPartner PartnerByErpNumber(PartnerRole role, string erpNumber)
        {
            return Directory.FindByErpNumber(role, erpNumber);
        }

        public string ToInternal(string partnerId, CrossReferenceKind kind, string partnerValue)
        {
            return Directory.Translate(partnerId, kind, partnerValue, DateTime.Today);
        }

        public string ToPartner(string partnerId, CrossReferenceKind kind, string internalValue)
        {
            return Directory.TranslateBack(partnerId, kind, internalValue, DateTime.Today);
        }
    }
}
