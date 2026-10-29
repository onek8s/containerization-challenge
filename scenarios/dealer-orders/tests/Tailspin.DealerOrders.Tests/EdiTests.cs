using System;
using System.Linq;
using Tailspin.DealerOrders.Edi.Maps;
using Tailspin.DealerOrders.Edi.X12;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class EdiTests
    {
        [Fact]
        public void Reads_850_with_configured_machine_and_parts()
        {
            var interchange = X12Reader.Read(Lab.Sample("edi/850-glf-machine.x12"), out var errors);
            Assert.Empty(errors);
            Assert.Equal("078912345", interchange.SenderId);
            Assert.True(interchange.AcknowledgmentRequested);
            var po = PurchaseOrder850.Map(interchange.Groups[0].Transactions[0]);
            Assert.Equal("GLF-PO-48213", po.PoNumber);
            Assert.Equal(PoPurpose.Original, po.Purpose);
            Assert.Equal(new DateTime(2019, 12, 19), po.RequestedDelivery);
            Assert.Equal("GLF-DUL", po.ShipToCode);
            Assert.Equal("Arrowhead Timber LLC", po.EndCustomer);
            Assert.Equal(3, po.Lines.Count);
            Assert.Equal("TF14", po.Lines[0].VendorPartNumber);
            Assert.Equal(new[] { "OPT-ENGT4F", "OPT-CRANE11", "OPT-BOGIEHD", "OPT-LED", "OPT-TRKECO" }, po.Lines[0].OptionItemIds);
            Assert.Equal(24m, po.Lines[2].Quantity);
            Assert.Equal("GLF-HF45", po.Lines[2].BuyerPartNumber);
        }

        [Fact]
        public void Segment_count_mismatch_is_reported_as_error()
        {
            X12Reader.Read(Lab.Sample("edi/850-glf-bad-count.x12"), out var errors);
            var error = Assert.Single(errors);
            Assert.Equal(X12ErrorLevel.Transaction, error.Level);
            Assert.Equal("4", error.Code);
        }

        [Fact]
        public void Delimiters_are_taken_from_isa()
        {
            var text = Lab.Sample("edi/850-ntm-parts.x12").Replace('*', '|').Replace('~', '\'');
            var interchange = X12Reader.Read(text, out var errors);
            Assert.Empty(errors);
            Assert.Equal('|', interchange.Delimiters.Element);
            Assert.Equal("NTM-77120", PurchaseOrder850.Map(interchange.Groups[0].Transactions[0]).PoNumber);
        }

        [Fact]
        public void Writer_output_reads_back_with_correct_envelopes()
        {
            var ack = new PoAcknowledgment { PoNumber = "PO-1", PoDate = new DateTime(2019, 10, 14), OrderNumber = "SO19-004501", AckDate = new DateTime(2019, 10, 15), Currency = "USD" };
            ack.Lines.Add(new PoAckLine { LineId = "1", OrderedQuantity = 1, AcceptedQuantity = 1, UnitPrice = 249500m, VendorPartNumber = "TF14", ScheduledShipDate = new DateTime(2020, 1, 23), Code = LineAckCode.DateRescheduled });
            ack.Lines.Add(new PoAckLine { LineId = "2", OrderedQuantity = 2, AcceptedQuantity = 0, UnitPrice = 0, VendorPartNumber = "P-99999", Code = LineAckCode.Rejected });
            var writer = new X12Writer();
            writer.AddTransaction("855", PoAcknowledgment855.Build(ack));
            var text = writer.Write(new X12Party("ZZ", "TAILSPINMACH", "TAILSPIN"), new X12Party("01", "078912345", "GLFEQUIP"), 1001, 1001, "PR",
                new DateTime(2019, 10, 15, 9, 30, 0), test: false, requestAck: true);

            var back = X12Reader.Read(text, out var errors);
            Assert.Empty(errors);
            Assert.Equal(106, text.IndexOf('\n'));
            var t = back.Groups[0].Transactions[0];
            Assert.Equal("AC", t.First("BAK")[2]);
            Assert.Equal(new[] { "DR", "IR" }, t.All("ACK").Select(a => a[1]));
            Assert.Equal("20200123", t.First("ACK")[5]);
        }

        [Fact]
        public void Ship_notice_has_shipment_order_item_hierarchy()
        {
            var notice = new ShipNotice { ShipmentId = "PS19-000001", ShipDate = new DateTime(2019, 12, 12, 14, 5, 0), CarrierScac = "ODFL", PoNumber = "PO-1", PoDate = new DateTime(2019, 10, 14), Packages = 1, GrossWeightKg = 18500m, ShipFromName = "Tailspin", ShipToName = "GLF", ShipToCode = "GLF-DUL" };
            notice.Lines.Add(new ShipNoticeLine { LineId = "1", VendorPartNumber = "TF14", Quantity = 1, OrderedQuantity = 1, SerialNumber = "TF141910001" });
            notice.Lines.Add(new ShipNoticeLine { LineId = "2", VendorPartNumber = "P-20310", BuyerPartNumber = "GLF-R14", Quantity = 2, OrderedQuantity = 2 });
            var segments = ShipNotice856.Build(notice);
            var hl = segments.Where(s => s.Id == "HL").Select(s => s[3]).ToArray();
            Assert.Equal(new[] { "S", "O", "I", "I" }, hl);
            Assert.Equal("4", segments.Single(s => s.Id == "CTT")[1]);
            Assert.Equal("TF141910001", segments.First(s => s.Id == "LIN").ValueAfterQualifier("SN", 2));
        }

        [Fact]
        public void Functional_ack_rejects_bad_transaction()
        {
            var interchange = X12Reader.Read(Lab.Sample("edi/850-glf-bad-count.x12"), out var errors);
            var segments = FunctionalAck997.Build(interchange.Groups[0], errors);
            Assert.Equal("R", segments.Single(s => s.Id == "AK5")[1]);
            Assert.Equal("4", segments.Single(s => s.Id == "AK5")[2]);
            Assert.Equal(new[] { "R", "1", "1", "0" }, segments.Single(s => s.Id == "AK9").Elements.Take(4));
        }

        [Fact]
        public void Bad_purpose_and_missing_lines_are_format_errors()
        {
            var text = Lab.Sample("edi/850-glf-cancel.x12").Replace("BEG*01", "BEG*99");
            var interchange = X12Reader.Read(text, out _);
            Assert.Throws<X12FormatException>(() => PurchaseOrder850.Map(interchange.Groups[0].Transactions[0]));
            Assert.Throws<X12FormatException>(() => X12Reader.Read("hello", out _));
        }
    }
}
