using System;
using System.Collections.Generic;
using System.Data;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Data
{
    public class PartnerRepository
    {
        private readonly IntegrationDatabase _db;

        public PartnerRepository(IntegrationDatabase db)
        {
            _db = db;
        }

        public IList<TradingPartner> LoadPartners()
        {
            var list = new List<TradingPartner>();
            using (var cmd = _db.Procedure("dbo.usp_GetTradingPartners"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new TradingPartner
                    {
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        Name = IntegrationDatabase.S(r, "Name"),
                        Role = (PartnerRole)IntegrationDatabase.I(r, "RoleId"),
                        Standard = (DocumentStandard)IntegrationDatabase.I(r, "StandardId"),
                        Transport = (TransportKind)IntegrationDatabase.I(r, "TransportId"),
                        InterchangeQualifier = IntegrationDatabase.S(r, "InterchangeQualifier"),
                        InterchangeId = IntegrationDatabase.S(r, "InterchangeId"),
                        Version = IntegrationDatabase.S(r, "StandardVersion"),
                        Gln = IntegrationDatabase.S(r, "Gln"),
                        ErpNumber = IntegrationDatabase.S(r, "ErpNumber"),
                        As2Identifier = IntegrationDatabase.S(r, "As2Identifier"),
                        CurrencyCode = IntegrationDatabase.S(r, "CurrencyCode"),
                        CountryCode = IntegrationDatabase.S(r, "CountryCode"),
                        DefaultShipVia = IntegrationDatabase.S(r, "DefaultShipVia"),
                        AckRequired = IntegrationDatabase.B(r, "AckRequired"),
                        AckTimeoutMinutes = IntegrationDatabase.I(r, "AckTimeoutMinutes"),
                        CreditCheckThreshold = IntegrationDatabase.M(r, "CreditCheckThreshold"),
                        Active = IntegrationDatabase.B(r, "Active"),
                        TestIndicator = IntegrationDatabase.B(r, "TestIndicator")
                    });
                }
            }

            return list;
        }

        public IList<PartnerCrossReference> LoadCrossReferences()
        {
            var list = new List<PartnerCrossReference>();
            using (var cmd = _db.Procedure("dbo.usp_GetPartnerXref"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new PartnerCrossReference
                    {
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        Kind = (CrossReferenceKind)IntegrationDatabase.I(r, "KindId"),
                        PartnerValue = IntegrationDatabase.S(r, "PartnerValue"),
                        InternalValue = IntegrationDatabase.S(r, "InternalValue"),
                        ValidFrom = IntegrationDatabase.D(r, "ValidFrom"),
                        ValidTo = IntegrationDatabase.D(r, "ValidTo")
                    });
                }
            }

            return list;
        }

        public PartnerDirectory LoadDirectory()
        {
            return new PartnerDirectory(LoadPartners(), LoadCrossReferences());
        }

        public void SaveCrossReference(PartnerCrossReference x, string changedBy)
        {
            using (var cmd = _db.Procedure("dbo.usp_SavePartnerXref"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, x.PartnerId);
                _db.In(cmd, "@KindId", DbType.Int32, (int)x.Kind);
                _db.In(cmd, "@PartnerValue", DbType.String, x.PartnerValue);
                _db.In(cmd, "@InternalValue", DbType.String, x.InternalValue);
                _db.In(cmd, "@ValidFrom", DbType.Date, x.ValidFrom);
                _db.In(cmd, "@ValidTo", DbType.Date, x.ValidTo);
                _db.In(cmd, "@ChangedBy", DbType.String, changedBy);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public void DeleteCrossReference(string partnerId, CrossReferenceKind kind, string partnerValue, string changedBy)
        {
            using (var cmd = _db.Procedure("dbo.usp_DeletePartnerXref"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@KindId", DbType.Int32, (int)kind);
                _db.In(cmd, "@PartnerValue", DbType.String, partnerValue);
                _db.In(cmd, "@ChangedBy", DbType.String, changedBy);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public void SetPartnerActive(string partnerId, bool active, string changedBy)
        {
            using (var cmd = _db.Procedure("dbo.usp_SetPartnerActive"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@Active", DbType.Boolean, active);
                _db.In(cmd, "@ChangedBy", DbType.String, changedBy);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }
    }
}
