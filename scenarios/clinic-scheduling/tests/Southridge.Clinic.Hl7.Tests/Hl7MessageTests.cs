using System;
using System.Collections.Generic;
using Xunit;

namespace Southridge.Clinic.Hl7.Tests
{
    public class Hl7MessageTests
    {
        private const string Adt =
            "MSH|^~\\&|STCADT|STCATH|SMGIE|SMG|20130312091500||ADT^A04|STC000123|P|2.3\r" +
            "EVN|A04|201303120915\r" +
            "PID|1||H448812^^^STC^PI~1045230-1^^^SMG^MR||LINDQVIST^MARIA^E||19710402|F|||418 N YAKIMA AVE^APT 2^TACOMA^WA^98403||(253)555-0144^^^maria@example.net\r" +
            "PV1|1|E|ED^^^STCATH||||1234567893^BRENNAN^ANNA||||||||||||V9912||||||||||||||||||||||||201303120900\r";

        [Fact]
        public void ParsesHeaderFields()
        {
            Hl7Message m = Hl7Message.Parse(Adt);

            Assert.Equal("ADT", m.MessageType);
            Assert.Equal("A04", m.TriggerEvent);
            Assert.Equal("ADT^A04", m.TypeAndTrigger);
            Assert.Equal("STC000123", m.ControlId);
            Assert.Equal("2.3", m.Version);
            Assert.Equal("STCADT", m.Get("MSH-3"));
        }

        [Theory]
        [InlineData("PID-5-1", "LINDQVIST")]
        [InlineData("PID-5-2", "MARIA")]
        [InlineData("PID-11-3", "TACOMA")]
        [InlineData("PID-13-4", "maria@example.net")]
        [InlineData("PV1-7-2", "BRENNAN")]
        [InlineData("PV1-19", "V9912")]
        [InlineData("ZZZ-1", "")]
        public void ReadsFieldsByPath(string path, string expected)
        {
            Assert.Equal(expected, Hl7Message.Parse(Adt).Get(path));
        }

        [Fact]
        public void PatientReaderSplitsClinicAndHospitalIdentifiers()
        {
            Hl7Patient p = MessageReader.ReadPatient(Hl7Message.Parse(Adt), "STC");

            Assert.Equal("1045230-1", p.Mrn);
            Assert.Equal("H448812", p.ExternalId);
            Assert.Equal("STC", p.ExternalAuthority);
            Assert.Equal(new DateTime(1971, 4, 2), p.DateOfBirth);
            Assert.Equal("APT 2", p.Address2);
        }

        [Fact]
        public void PatientWithoutDateOfBirthIsRejected()
        {
            Hl7Message m = Hl7Message.Parse(Adt);
            m.Set("PID-7", "");

            Assert.Throws<Hl7Exception>(() => MessageReader.ReadPatient(m, "STC"));
        }

        [Fact]
        public void SetCreatesMissingSegmentsAndComponents()
        {
            Hl7Message m = Hl7Message.Parse(Adt);
            m.Set("ZSM-3-2", "VIP");
            m.Set("PID-8", "U");

            Hl7Message reparsed = Hl7Message.Parse(m.ToString());
            Assert.Equal("VIP", reparsed.Get("ZSM-3-2"));
            Assert.Equal("U", reparsed.Get("PID-8"));
        }

        [Theory]
        [InlineData("Smith & Jones|Co", "Smith \\T\\ Jones\\F\\Co")]
        [InlineData("50^60", "50\\S\\60")]
        [InlineData("a~b\\c", "a\\R\\b\\E\\c")]
        public void EscapesDelimitersRoundTrip(string raw, string escaped)
        {
            Assert.Equal(escaped, Hl7Encoding.Default.Escape(raw));
            Assert.Equal(raw, Hl7Encoding.Default.Unescape(escaped));
        }

        [Theory]
        [InlineData("20130312", 2013, 3, 12, 0, 0)]
        [InlineData("201303120915", 2013, 3, 12, 9, 15)]
        [InlineData("20130312091522-0800", 2013, 3, 12, 9, 15)]
        public void ParsesDateTimes(string value, int y, int mo, int d, int h, int mi)
        {
            DateTime? parsed = Hl7DateTime.Parse(value);

            Assert.True(parsed.HasValue);
            Assert.Equal(new DateTime(y, mo, d, h, mi, 0), new DateTime(parsed.Value.Year, parsed.Value.Month, parsed.Value.Day, parsed.Value.Hour, parsed.Value.Minute, 0));
        }

        [Fact]
        public void UnparseableDateIsNull()
        {
            Assert.Null(Hl7DateTime.Parse("31/12/2013"));
            Assert.Null(Hl7DateTime.Parse(""));
        }

        [Fact]
        public void AcknowledgementSwapsSenderAndReceiver()
        {
            Hl7Message received = Hl7Message.Parse(Adt);
            Hl7Message ack = Acknowledgement.Create(received, AckCode.Error, "PID-7 date of birth is required", "SMG0001");

            Assert.Equal("ACK", ack.MessageType);
            Assert.Equal("SMGIE", ack.Get("MSH-3"));
            Assert.Equal("STCADT", ack.Get("MSH-5"));
            Assert.Equal("STC000123", ack.Get("MSA-2"));
            Assert.Equal("AE", ack.Get("MSA-1"));
            Assert.False(Acknowledgement.IsPositive(ack));
            Assert.True(Acknowledgement.IsPositive(Acknowledgement.Create(received, AckCode.Accept, null, "SMG0002")));
        }
    }
}
