using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Data.Mapping
{
    internal class DealerMap : EntityTypeConfiguration<Dealer>
    {
        public DealerMap()
        {
            ToTable("Dealer", "dlr");
            Property(d => d.AccountNumber).HasMaxLength(20).IsRequired();
            Property(d => d.Name).HasMaxLength(100).IsRequired();
            Property(d => d.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            Property(d => d.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            Property(d => d.LanguageCode).HasMaxLength(5).IsUnicode(false);
            Property(d => d.PriceGroup).HasMaxLength(10);
            Property(d => d.PaymentTerms).HasMaxLength(10);
            Property(d => d.Warehouse).HasMaxLength(10);
            Property(d => d.SalesDistrict).HasMaxLength(10);
            Property(d => d.SalesResponsible).HasMaxLength(60);
            Property(d => d.MainDealerAccount).HasMaxLength(20);
            Property(d => d.Email).HasMaxLength(254);
            Property(d => d.Phone).HasMaxLength(30);
            Property(d => d.RowVersion).IsRowVersion();
            HasMany(d => d.Addresses).WithRequired().HasForeignKey(a => a.DealerId);
        }
    }

    internal class DealerAddressMap : EntityTypeConfiguration<DealerAddress>
    {
        public DealerAddressMap()
        {
            ToTable("DealerAddress", "dlr");
            Property(a => a.AxLocationId).HasMaxLength(20);
            Property(a => a.Name).HasMaxLength(100).IsRequired();
            Property(a => a.Street).HasMaxLength(250);
            Property(a => a.PostalCode).HasMaxLength(10);
            Property(a => a.City).HasMaxLength(60);
            Property(a => a.State).HasMaxLength(10);
            Property(a => a.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            Property(a => a.EdiLocationCode).HasMaxLength(20);
        }
    }

    internal class QuoteMap : EntityTypeConfiguration<Quote>
    {
        public QuoteMap()
        {
            ToTable("Quote", "dlr");
            Property(q => q.QuoteNumber).HasMaxLength(20).IsRequired();
            Property(q => q.CreatedBy).HasMaxLength(256);
            Property(q => q.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            Property(q => q.EndCustomerName).HasMaxLength(100);
            Property(q => q.EndCustomerCountry).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            Property(q => q.Reference).HasMaxLength(60);
            Property(q => q.AxQuotationId).HasMaxLength(20);
            Property(q => q.ApprovalComment).HasMaxLength(1000);
            Property(q => q.ValidUntil).HasColumnType("date");
            Property(q => q.RowVersion).IsRowVersion();
            HasRequired(q => q.Dealer).WithMany().HasForeignKey(q => q.DealerId);
            HasMany(q => q.Lines).WithRequired().HasForeignKey(l => l.QuoteId).WillCascadeOnDelete(true);
            Ignore(q => q.TotalNet);
            Ignore(q => q.TotalList);
            Ignore(q => q.CanEdit);
        }
    }

    internal class QuoteLineMap : EntityTypeConfiguration<QuoteLine>
    {
        public QuoteLineMap()
        {
            ToTable("QuoteLine", "dlr");
            Property(l => l.ItemId).HasMaxLength(20).IsRequired();
            Property(l => l.ModelCode).HasMaxLength(20);
            Property(l => l.ConfigurationId).HasMaxLength(10);
            Property(l => l.OptionCodes).HasMaxLength(1000);
            Property(l => l.Description).HasMaxLength(200);
            Ignore(l => l.ListAmount);
            Ignore(l => l.NetAmount);
            Ignore(l => l.Options);
        }
    }

    internal class SalesOrderMap : EntityTypeConfiguration<SalesOrder>
    {
        public SalesOrderMap()
        {
            ToTable("SalesOrder", "dlr");
            Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
            Property(o => o.DealerPoNumber).HasMaxLength(40);
            Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            Property(o => o.CreatedBy).HasMaxLength(256);
            Property(o => o.EndCustomerName).HasMaxLength(100);
            Property(o => o.AxSalesId).HasMaxLength(20);
            Property(o => o.AxStatusText).HasMaxLength(100);
            Property(o => o.ErrorMessage).HasMaxLength(2000);
            Property(o => o.Notes).HasMaxLength(4000);
            Property(o => o.RequestedDeliveryDate).HasColumnType("date");
            Property(o => o.ConfirmedDeliveryDate).HasColumnType("date");
            Property(o => o.RowVersion).IsRowVersion();
            HasRequired(o => o.Dealer).WithMany().HasForeignKey(o => o.DealerId);
            HasOptional(o => o.DeliveryAddress).WithMany().HasForeignKey(o => o.DeliveryAddressId);
            HasMany(o => o.Lines).WithRequired().HasForeignKey(l => l.OrderId).WillCascadeOnDelete(true);
            HasMany(o => o.History).WithRequired().HasForeignKey(h => h.OrderId).WillCascadeOnDelete(true);
            Ignore(o => o.TotalNet);
            Ignore(o => o.OrderValue);
            Ignore(o => o.IsOpen);
            Ignore(o => o.CanCancel);
            Ignore(o => o.CanChange);
        }
    }

    internal class OrderLineMap : EntityTypeConfiguration<OrderLine>
    {
        public OrderLineMap()
        {
            ToTable("OrderLine", "dlr");
            Property(l => l.ItemId).HasMaxLength(20).IsRequired();
            Property(l => l.ModelCode).HasMaxLength(20);
            Property(l => l.ConfigurationId).HasMaxLength(10);
            Property(l => l.OptionCodes).HasMaxLength(1000);
            Property(l => l.Description).HasMaxLength(200);
            Property(l => l.DealerLineReference).HasMaxLength(20);
            Property(l => l.DealerItemNumber).HasMaxLength(40);
            Property(l => l.AxInventTransId).HasMaxLength(20);
            Property(l => l.SerialNumber).HasMaxLength(30);
            Property(l => l.ConfirmedShipDate).HasColumnType("date");
            Ignore(l => l.NetAmount);
            Ignore(l => l.OpenQuantity);
        }
    }

    internal class OrderStatusChangeMap : EntityTypeConfiguration<OrderStatusChange>
    {
        public OrderStatusChangeMap()
        {
            ToTable("OrderStatusChange", "dlr");
            Property(h => h.ChangedBy).HasMaxLength(256);
            Property(h => h.Comment).HasMaxLength(1000);
        }
    }

    internal class ApprovalMap : EntityTypeConfiguration<ApprovalRequest>
    {
        public ApprovalMap()
        {
            ToTable("Approval", "dlr");
            Property(a => a.Details).HasMaxLength(1000);
            Property(a => a.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            Property(a => a.RequestedBy).HasMaxLength(256);
            Property(a => a.DecidedBy).HasMaxLength(256);
            Property(a => a.DecisionComment).HasMaxLength(1000);
        }
    }

    internal class ShipmentMap : EntityTypeConfiguration<Shipment>
    {
        public ShipmentMap()
        {
            ToTable("Shipment", "dlr");
            Property(s => s.PackingSlipId).HasMaxLength(20).IsRequired();
            Property(s => s.CarrierCode).HasMaxLength(10);
            Property(s => s.TrackingNumber).HasMaxLength(40);
            Property(s => s.TrailerNumber).HasMaxLength(20);
            Property(s => s.ShipDate).HasColumnType("date");
            HasMany(s => s.Lines).WithRequired().HasForeignKey(l => l.ShipmentId).WillCascadeOnDelete(true);
        }
    }

    internal class ShipmentLineMap : EntityTypeConfiguration<ShipmentLine>
    {
        public ShipmentLineMap()
        {
            ToTable("ShipmentLine", "dlr");
            Property(l => l.ItemId).HasMaxLength(20);
            Property(l => l.SerialNumber).HasMaxLength(30);
        }
    }

    internal class AxCommandMap : EntityTypeConfiguration<AxCommand>
    {
        public AxCommandMap()
        {
            ToTable("Command", "ax");
            Property(c => c.SequenceKey).HasMaxLength(40).IsRequired();
            Property(c => c.RequestedBy).HasMaxLength(256);
            Property(c => c.RunAsAxUser).HasMaxLength(20);
            Property(c => c.LastError).HasMaxLength(4000);
            Property(c => c.Result).HasMaxLength(1000);
        }
    }

    internal class NotificationMap : EntityTypeConfiguration<Notification>
    {
        public NotificationMap()
        {
            ToTable("Notification", "dlr");
            Property(n => n.Recipient).HasMaxLength(254);
            Property(n => n.Subject).HasMaxLength(200);
            Property(n => n.LastError).HasMaxLength(1000);
        }
    }

    internal class EdiPartnerMap : EntityTypeConfiguration<EdiPartner>
    {
        public EdiPartnerMap()
        {
            ToTable("Partner", "edi");
            Property(p => p.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            Property(p => p.Name).HasMaxLength(60).IsRequired();
            Property(p => p.InterchangeQualifier).HasMaxLength(2);
            Property(p => p.InterchangeId).HasMaxLength(15);
            Property(p => p.ApplicationCode).HasMaxLength(15);
            Property(p => p.As2Id).HasMaxLength(60);
            Property(p => p.As2Url).HasMaxLength(250);
            Property(p => p.PartnerCertificateFile).HasMaxLength(260);
            Property(p => p.SftpHost).HasMaxLength(100);
            Property(p => p.SftpUser).HasMaxLength(60);
            Property(p => p.SftpInboundFolder).HasMaxLength(100);
            Property(p => p.SftpOutboundFolder).HasMaxLength(100);
        }
    }

    internal class EdiMessageMap : EntityTypeConfiguration<EdiMessage>
    {
        public EdiMessageMap()
        {
            ToTable("Message", "edi");
            Property(m => m.TransactionSet).HasMaxLength(20);
            Property(m => m.InterchangeControl).HasMaxLength(9);
            Property(m => m.GroupControl).HasMaxLength(60);
            Property(m => m.TransactionControl).HasMaxLength(9);
            Property(m => m.Reference).HasMaxLength(100);
            Property(m => m.As2MessageId).HasMaxLength(200);
            Property(m => m.MdnStatus).HasMaxLength(200);
            Property(m => m.Error).HasMaxLength(2000);
        }
    }

    internal class StoredDocumentMap : EntityTypeConfiguration<StoredDocument>
    {
        public StoredDocumentMap()
        {
            ToTable("Document", "dlr");
            Property(d => d.FileName).HasMaxLength(200).IsRequired();
            Property(d => d.ContentType).HasMaxLength(100);
            Property(d => d.Sha256).HasMaxLength(64).IsFixedLength().IsUnicode(false);
            Property(d => d.CreatedBy).HasMaxLength(256);
        }
    }
}
