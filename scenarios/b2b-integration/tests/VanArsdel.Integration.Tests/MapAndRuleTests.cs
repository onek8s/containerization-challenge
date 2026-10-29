using System.Xml;
using VanArsdel.Integration.Contracts.Canonical;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.LabStubs.Simulators;
using VanArsdel.Integration.Rules;
using Xunit;

namespace VanArsdel.Integration.Tests
{
    public class MapAndRuleTests
    {
        private static Order MapOrder(TestLab t, string sample, string map, int set = 0)
        {
            var dis = EdiDisassembler.Disassemble(TestLab.SampleText(sample));
            var canonical = t.Lab.Map(map).Execute(dis.Transactions[set].Document);
            return CanonicalSerializer.Deserialize<Order>(canonical);
        }

        [Fact]
        public void X12_850_maps_partner_items_ship_to_and_units()
        {
            using var t = new TestLab();

            var order = MapOrder(t, "C1001_850_HW-55012.x12", "X12_00401_850_to_Order");

            Assert.Equal("C1001", order.Header.PartnerId);
            Assert.Equal("HW-55012", order.Header.OrderNumber);
            Assert.Equal("104711", order.Header.ErpPartnerNumber);
            Assert.Equal(new[] { "EL-10020", "EL-10032", "CB-50125", "EL-30115" }, order.Lines.Select(l => l.Item));
            Assert.Equal("M", order.Lines[2].Uom);
            Assert.Equal("604201", order.Parties.Single(p => p.Role == "ST").InternalId);
        }

        [Fact]
        public void Edifact_orders_maps_gln_delivery_point()
        {
            using var t = new TestLab();

            var order = MapOrder(t, "C2001_ORDERS_BW-778812.edi", "EFACT_D96A_ORDERS_to_Order");

            Assert.Equal("C2001", order.Header.PartnerId);
            Assert.Equal("EUR", order.Header.Currency);
            Assert.Equal("704101", order.Parties.Single(p => p.Role == "ST").InternalId);
            Assert.Equal("EL-10020", order.Lines[0].Item);
        }

        [Fact]
        public void Validation_policy_flags_unknown_items_for_manual_review()
        {
            using var t = new TestLab();
            var order = MapOrder(t, "C1001_850_HW-55012.x12", "X12_00401_850_to_Order", 1);
            var outcome = new RuleOutcome();

            t.Lab.Policy("VanArsdel.OrderValidation").Execute(new OrderFacts(order), new PartnerFacts(t.Lab.Directory.Get("C1001")),
                new CalendarFacts(t.Lab.Now, 15, Array.Empty<DateTime>()), outcome);

            Assert.True(outcome.ManualReview);
            Assert.Contains("UnknownItem", outcome.FiredRules);
        }

        [Fact]
        public void Validation_policy_sets_default_ship_via_and_cable_drum_note()
        {
            using var t = new TestLab();
            var order = MapOrder(t, "C1001_850_HW-55012.x12", "X12_00401_850_to_Order");
            var outcome = new RuleOutcome();

            t.Lab.Policy("VanArsdel.OrderValidation").Execute(new OrderFacts(order), new PartnerFacts(t.Lab.Directory.Get("C1001")),
                new CalendarFacts(new DateTime(2026, 3, 2, 16, 30, 0), 15, Array.Empty<DateTime>()), outcome);

            Assert.False(outcome.Rejected);
            Assert.Equal("UPSGND", outcome.ShipVia);
            Assert.Contains(outcome.Notes, n => n.StartsWith("Cable over 500 m", StringComparison.Ordinal));
            Assert.Contains("AfterCutOff", outcome.FiredRules);
            Assert.False(outcome.CreditCheckRequired);
        }

        [Fact]
        public void Credit_policy_holds_a_blocked_account()
        {
            using var t = new TestLab();
            var partner = new PartnerFacts(t.Lab.Directory.Get("C3002")) { CreditChecked = true, CreditApproved = false, BlockReason = "Overdue invoices" };
            var order = MapOrder(t, "C1001_850_HW-55012.x12", "X12_00401_850_to_Order");
            var outcome = new RuleOutcome();

            t.Lab.Policy("VanArsdel.CreditRelease").Execute(new OrderFacts(order), partner, outcome);

            Assert.True(outcome.CreditHold);
            Assert.Equal("Overdue invoices", outcome.HoldReason);
        }

        [Theory]
        [InlineData("C1001", "Despatch", "0|MAP:Despatch_to_X12_00401_856;SEND:AS2.C1001;ARCHIVE:outbound;TRACK:Despatch")]
        [InlineData("C2001", "Invoice", "0|MAP:Invoice_to_EFACT_D96A_INVOIC;SEND:AS2.C2001;ARCHIVE:outbound;TRACK:Invoice")]
        [InlineData("C2002", "Despatch", "0|ARCHIVE:outbound;TRACK:Despatch")]
        [InlineData("S5001", "PurchaseOrder", "0|MAP:Order_to_EFACT_D96A_ORDERS;SEND:SFTP.S5001;ARCHIVE:outbound;TRACK:PurchaseOrder")]
        public void Routing_policy_builds_the_slip(string partnerId, string kind, string expected)
        {
            using var t = new TestLab();
            var p = t.Lab.Directory.Get(partnerId);
            var facts = new RoutingFacts(partnerId, p.Standard.ToString(), p.Transport.ToString(), kind);

            t.Lab.Policy("VanArsdel.Routing").Execute(facts);

            Assert.Equal(expected, facts.Slip);
        }

        [Fact]
        public void Erp_despatch_idoc_maps_to_despatch_with_erp_order_and_packages()
        {
            using var t = new TestLab();
            var erp = new ErpSystem(t.Lab);
            var idoc = t.Lab.Map("Order_to_IDoc_ORDERS05").Execute(Xml(CanonicalSerializer.Serialize(MapOrder(t, "C1001_850_HW-55012.x12", "X12_00401_850_to_Order"))));
            idoc.Save(t.Lab.WorkPath("erp", "inbound", "ORDERS05_test.xml"));
            Assert.Equal(1, erp.ProcessInbound());

            var file = Assert.Single(erp.GoodsIssue());
            var despatch = CanonicalSerializer.Deserialize<Despatch>(t.Lab.Map("IDoc_DESADV01_to_Despatch").Execute(Load(file)));

            Assert.Equal("C1001", despatch.Header.PartnerId);
            Assert.Equal("HW-55012", despatch.Header.OrderNumber);
            Assert.Equal("45001", despatch.Header.ErpOrderNumber);
            Assert.Equal("UPSGND", despatch.Header.Carrier);
            Assert.Equal(2, despatch.Packages.Length);
            Assert.Equal("0042", despatch.Parties.Single(p => p.Role == "ST").PartnerCode);
        }

        private static XmlDocument Xml(string text)
        {
            var d = new XmlDocument();
            d.LoadXml(text);
            return d;
        }

        private static XmlDocument Load(string file)
        {
            var d = new XmlDocument();
            d.Load(file);
            return d;
        }
    }
}
