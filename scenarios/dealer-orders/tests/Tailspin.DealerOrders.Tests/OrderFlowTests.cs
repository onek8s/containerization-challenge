using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Edi.X12;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Sales;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class OrderFlowTests
    {
        private static ConfigurationRequest Tf14(params string[] options) =>
            new ConfigurationRequest { ModelCode = "TF14", Options = options.ToList(), Quantity = 1 };

        [Fact]
        public void Quote_with_extra_discount_goes_through_approval_registration_and_becomes_an_ax_order()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-10010");
            var quote = lab.Quotes.CreateFromConfiguration(dealer.Id, "mikko@mkp", Tf14("CRANE-11M", "BOGIE-HD", "LED-PACK"), "Lapin Metsäpalvelu Oy", "FI", "MKP-1907");
            Assert.Equal("Q-2019-000001", quote.QuoteNumber);
            Assert.Equal(new DateTime(2019, 11, 14), quote.ValidUntil);

            lab.Quotes.Submit(quote.Id, "mikko@mkp", 5m);
            Assert.Equal(QuoteStatus.PendingApproval, quote.Status);
            var approval = Assert.Single(lab.Stores.Pending());
            Assert.Contains(lab.Stores.Notifications, n => n.Kind == NotificationKind.ApprovalNeeded && n.Recipient == lab.Settings.SalesDeskEmail);

            lab.Approvals.Decide(approval.Id, "CORP\\anna.virtanen", isManager: false, approve: true, discount: 4m, comment: "Competitive deal", axUser: "annvir");
            Assert.Equal(QuoteStatus.Approved, quote.Status);
            Assert.Equal(4m, quote.ApprovedExtraDiscount);

            lab.RunAx();
            Assert.Equal(QuoteStatus.RegisteredInAx, quote.Status);
            Assert.StartsWith("SQ19-", quote.AxQuotationId);

            var order = lab.Orders.AcceptQuote(quote.Id, "mikko@mkp", "MKP-PO-5521", null, null);
            Assert.Equal(OrderStatus.SentToAx, order.Status);
            Assert.Equal(Math.Round(quote.Lines.Single().NetPrice * 0.96m, 2), order.Lines.Single().NetPrice);

            lab.RunAx();
            Assert.Equal(OrderStatus.CreatedInAx, order.Status);
            Assert.StartsWith("SO19-", order.AxSalesId);
            Assert.Equal(QuoteStatus.Converted, quote.Status);

            lab.Elapse(TimeSpan.FromMinutes(3));
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.NotNull(order.ConfirmedDeliveryDate);
            Assert.Contains(lab.Stores.Notifications, n => n.Kind == NotificationKind.OrderConfirmed && n.OrderId == order.Id && n.Subject.Contains("vahvistettu"));

            lab.Elapse(TimeSpan.FromMinutes(3));
            Assert.Equal(OrderStatus.InProduction, order.Status);
            lab.Elapse(TimeSpan.FromMinutes(5));
            Assert.Equal(OrderStatus.Shipped, order.Status);
            Assert.StartsWith("TF1419", order.Lines.Single().SerialNumber);
            lab.Elapse(TimeSpan.FromMinutes(5));
            Assert.Equal(OrderStatus.Invoiced, order.Status);
            Assert.Equal(new[] { OrderStatus.Submitted, OrderStatus.Approved, OrderStatus.SentToAx, OrderStatus.CreatedInAx, OrderStatus.Confirmed, OrderStatus.InProduction, OrderStatus.Shipped, OrderStatus.Invoiced },
                order.History.Select(h => h.ToStatus));
        }

        [Fact]
        public void Large_discount_needs_a_manager()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-10045");
            var quote = lab.Quotes.CreateFromConfiguration(dealer.Id, "u", new ConfigurationRequest { ModelCode = "TH8", Options = new List<string> { "FIRE-SUPP" } }, null, null, null);
            lab.Quotes.Submit(quote.Id, "u", 12m);
            var approval = lab.Stores.Pending().Single();
            Assert.Throws<SalesRuleException>(() => lab.Approvals.Decide(approval.Id, "desk", false, true, null, null, null));
            lab.Approvals.Decide(approval.Id, "manager", true, true, null, "ok", null);
            Assert.Equal(QuoteStatus.Approved, quote.Status);
        }

        [Fact]
        public void Dealer_on_credit_hold_goes_to_credit_review_and_rejection_notifies_dealer()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-10051");
            var order = lab.Orders.CreateOrder(new NewOrderRequest
            {
                DealerId = dealer.Id, User = "u", DealerPoNumber = "EMT-1",
                Parts = { new PartLineRequest { ItemId = "P-10210", Quantity = 10 } }
            });
            Assert.Equal(OrderStatus.CreditHold, order.Status);
            Assert.Equal(ApprovalReason.CreditHold, lab.Stores.Pending().Single().Reason);
            Assert.Empty(lab.Stores.Commands);

            Assert.Throws<SalesRuleException>(() => lab.Approvals.Decide(lab.Stores.Pending().Single().Id, "fin", true, false, null, "", null));
            lab.Approvals.Decide(lab.Stores.Pending().Single().Id, "fin", true, false, null, "Overdue invoices", null);
            Assert.Equal(OrderStatus.Rejected, order.Status);
            Assert.Contains(lab.Stores.Notifications, n => n.Kind == NotificationKind.OrderRejected && n.Body.Contains("Overdue invoices"));
        }

        [Fact]
        public void Exposure_above_credit_limit_needs_approval_then_goes_to_ax()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-10045");
            var order = lab.Orders.CreateOrder(new NewOrderRequest
            {
                DealerId = dealer.Id, User = "u", DealerPoNumber = "FTB-77",
                Machines = { new ConfigurationRequest { ModelCode = "TF14", Quantity = 2 } }
            });
            Assert.Equal(OrderStatus.CreditHold, order.Status);
            Assert.Equal(ApprovalReason.CreditLimitExceeded, lab.Stores.Pending().Single().Reason);
            lab.Approvals.Decide(lab.Stores.Pending().Single().Id, "manager", true, true, null, "Bank guarantee received", "kanie");
            Assert.Equal(OrderStatus.SentToAx, order.Status);
            Assert.Equal("kanie", lab.Stores.Commands.Single().RunAsAxUser);
            lab.RunAx();
            Assert.Equal(OrderStatus.CreatedInAx, order.Status);
        }

        [Fact]
        public void Export_controlled_harvester_outside_free_trade_area_needs_approval()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-20007");
            var order = lab.Orders.CreateOrder(new NewOrderRequest
            {
                DealerId = dealer.Id, User = "u", DealerPoNumber = "GLF-1",
                Machines = { new ConfigurationRequest { ModelCode = "TH8", Options = { "ENG-T4F" } } }
            });
            Assert.Equal(OrderStatus.PendingApproval, order.Status);
            Assert.Equal(ApprovalReason.ExportControl, lab.Stores.Pending().Single().Reason);
        }

        [Fact]
        public void Parts_order_reserves_stock_and_duplicate_po_is_refused()
        {
            var lab = new Lab();
            var dealer = lab.Dealer("D-10010");
            var order = lab.Orders.CreateOrder(new NewOrderRequest
            {
                DealerId = dealer.Id, User = "u", DealerPoNumber = "MKP-P-1",
                Parts = { new PartLineRequest { ItemId = "P-10045", Quantity = 24 }, new PartLineRequest { ItemId = "P-20310", Quantity = 50 } }
            });
            Assert.Equal(OrderType.Parts, order.OrderType);
            Assert.Equal(41.25m, order.Lines.ElementAt(0).NetPrice);
            lab.RunAx();
            Assert.Equal(ReservationStatus.Reserved, order.Lines.ElementAt(0).Reservation);
            Assert.Equal(ReservationStatus.PartiallyReserved, order.Lines.ElementAt(1).Reservation);
            Assert.Equal(40m, order.Lines.ElementAt(1).ReservedQuantity);

            Assert.Throws<SalesRuleException>(() => lab.Orders.CreateOrder(new NewOrderRequest
            {
                DealerId = dealer.Id, User = "u", DealerPoNumber = "MKP-P-1", Parts = { new PartLineRequest { ItemId = "P-10046", Quantity = 1 } }
            }));
        }

        [Fact]
        public void Ax_outage_is_retried_and_business_error_puts_order_in_error_for_the_desk()
        {
            var lab = new Lab(unavailableEvery: 2);
            var dealer = lab.Dealer("D-10010");
            var order = lab.Orders.CreateOrder(new NewOrderRequest { DealerId = dealer.Id, User = "u", Parts = { new PartLineRequest { ItemId = "P-10046", Quantity = 2 } } });
            lab.RunAx();
            var create = lab.Stores.Commands.First();
            Assert.Equal(AxCommandState.Retry, create.State);
            Assert.Equal(lab.Clock.UtcNow.AddMinutes(1), create.NextAttemptUtc);
            Assert.Equal(0, lab.RunAx());
            lab.Clock.Advance(TimeSpan.FromMinutes(1));
            lab.RunAx();
            Assert.Equal(AxCommandState.Done, create.State);
            Assert.NotNull(order.AxSalesId);

            var lab2 = new Lab();
            var dealer2 = lab2.Dealer("D-10010");
            var bad = lab2.Orders.CreateOrder(new NewOrderRequest { DealerId = dealer2.Id, User = "u", Parts = { new PartLineRequest { ItemId = "P-10046", Quantity = 1 } } });
            bad.Lines.ElementAt(0).ItemId = "P-GONE";
            lab2.RunAx();
            Assert.Equal(OrderStatus.Error, bad.Status);
            Assert.Contains("Item number P-GONE does not exist.", bad.ErrorMessage);
            Assert.Contains(lab2.Stores.Notifications, n => n.Kind == NotificationKind.OrderError);
            // Reserve waits behind the failed create in the same sequence.
            Assert.Equal(AxCommandState.Pending, lab2.Stores.Commands.Last().State);

            bad.Lines.ElementAt(0).ItemId = "P-10046";
            lab2.Stores.Commands.First().State = AxCommandState.Cancelled;
            lab2.Stores.Commands.Last().State = AxCommandState.Cancelled;
            lab2.Orders.Resubmit(bad.Id, "desk", "annvir");
            lab2.RunAx();
            Assert.Equal(OrderStatus.CreatedInAx, bad.Status);
        }

        [Fact]
        public void Edi_850_creates_order_with_997_and_855_after_confirmation_and_856_after_shipping()
        {
            var lab = new Lab();
            var result = lab.EdiIn.Receive(Lab.Sample("edi/850-glf-machine.x12"), "<msg-1@glf>");
            Assert.Empty(result.Rejected);
            var order = lab.Stores.Orders.Single();
            Assert.Equal(OrderSource.Edi, order.Source);
            Assert.Equal("GLF-PO-48213", order.DealerPoNumber);
            Assert.Equal(3, order.Lines.Count);
            Assert.Equal("1", order.Lines.ElementAt(0).DealerLineReference);
            Assert.Equal("GLF-HF45", order.Lines.ElementAt(2).DealerItemNumber);
            Assert.Equal(6, order.DeliveryAddressId);
            Assert.Contains("ENG-T4F", order.Lines.ElementAt(0).OptionCodes);
            Assert.Equal(OrderStatus.SentToAx, order.Status);

            var ack997 = lab.Stores.Messages.Single(m => m.TransactionSet == "997");
            Assert.Contains("AK9*A*1*1*1", ack997.Content);

            lab.RunAx();
            lab.Elapse(TimeSpan.FromMinutes(3));
            var ack855 = lab.Stores.Messages.Single(m => m.TransactionSet == "855");
            var t855 = X12Reader.Read(ack855.Content, out var errors).Groups[0].Transactions[0];
            Assert.Empty(errors);
            Assert.Equal("GLF-PO-48213", t855.First("BAK")[3]);
            Assert.Equal(order.AxSalesId, t855.First("BAK")[8]);
            Assert.Equal(3, t855.All("ACK").Count());
            // The dealer's machine price differs from ours, so the line is acknowledged with a price change.
            Assert.Equal("IP", t855.All("ACK").First()[1]);

            lab.Elapse(TimeSpan.FromMinutes(10));
            var asn = lab.Stores.Messages.Single(m => m.TransactionSet == "856");
            Assert.Contains("PRF*GLF-PO-48213", asn.Content);
            Assert.Contains("*SN*TF1419", asn.Content);

            // A cancellation for a shipped order is refused and answered with a rejecting 855.
            var cancel = lab.EdiIn.Receive(Lab.Sample("edi/850-glf-cancel.x12"), "<msg-2@glf>");
            Assert.Contains("cannot be cancelled", Assert.Single(cancel.Rejected));
            Assert.Equal(OrderStatus.Shipped, order.Status);
        }

        [Fact]
        public void Edi_850_with_unknown_item_is_rejected_with_855()
        {
            var lab = new Lab();
            var result = lab.EdiIn.Receive(Lab.Sample("edi/850-glf-parts-unknown-item.x12"), null);
            Assert.Single(result.Rejected);
            Assert.Empty(lab.Stores.Orders);
            var ack = lab.Stores.Messages.Single(m => m.TransactionSet == "855");
            Assert.Contains("BAK*00*RJ*GLF-PO-48230", ack.Content);
            Assert.Contains("not sold to dealers", ack.Content);
        }

        [Fact]
        public void Edi_850_with_envelope_error_is_rejected_in_997_only()
        {
            var lab = new Lab();
            var result = lab.EdiIn.Receive(Lab.Sample("edi/850-glf-bad-count.x12"), null);
            Assert.Empty(lab.Stores.Orders);
            Assert.Single(result.EnvelopeErrors);
            Assert.Contains("AK5*R*4", lab.Stores.Messages.Single(m => m.TransactionSet == "997").Content);
        }

        [Fact]
        public void Cache_sync_reads_all_pages_and_updates_dealer_credit()
        {
            var lab = new Lab();
            Assert.True(lab.CacheSync.SyncPrices() > 100);
            Assert.True(lab.CacheSync.SyncStock() >= 20);
            Assert.Equal(5, lab.CacheSync.SyncExchangeRates());
            lab.CacheSync.SyncDealerBalances();
            Assert.Equal(5, lab.Stores.Stock("P-10045", null).Count + lab.Stores.Stock("P-10046", null).Count + 1);
        }

        [Fact]
        public void Notification_templates_render_subject_and_body()
        {
            var text = NotificationComposer.Render(NotificationKind.OrderShipped, "sv", new Dictionary<string, string> { ["OrderNumber"] = "WO-2019-000001", ["Carrier"] = "DSVR" });
            Assert.Equal("Order WO-2019-000001 shipped", text.Item1);
            Assert.Contains("Carrier: DSVR", text.Item2);
        }
    }
}

namespace Tailspin.DealerOrders.Tests
{
    public class TemplateCoverageTests
    {
        [Fact]
        public void Every_notification_kind_has_an_english_template()
        {
            foreach (NotificationKind kind in System.Enum.GetValues(typeof(NotificationKind)))
                Assert.NotEmpty(NotificationComposer.Render(kind, "en", new System.Collections.Generic.Dictionary<string, string>()).Item1);
        }
    }
}
