using System;
using System.Collections.Generic;
using Xunit;

namespace Southridge.Clinic.Hl7.Tests
{
    public class BuilderAndReaderTests
    {
        private static readonly Hl7Party Clinic = new Hl7Party("SMGPM", "SMG");
        private static readonly Hl7Party Lab = new Hl7Party("CRLAB", "CASCADE");

        private static Hl7Patient Patient()
        {
            return new Hl7Patient
            {
                Mrn = "1045230-1",
                LastName = "O'Brien",
                FirstName = "Sean",
                DateOfBirth = new DateTime(1968, 11, 30),
                Sex = "M",
                Address1 = "12 Pine & Elm",
                City = "Puyallup",
                State = "WA",
                Zip = "98371",
                HomePhone = "(253)555-0199"
            };
        }

        [Fact]
        public void LabOrderCarriesPlacerNumberAndTest()
        {
            var order = new Hl7Order { PlacerOrderNumber = "LO500123", TestCode = "A1C", TestName = "Hemoglobin A1c", OrderedAt = new DateTime(2013, 3, 12, 9, 40, 0), ClinicalInfo = "Fasting" };
            var provider = new Hl7Provider { Npi = "1234567893", LastName = "Brennan", FirstName = "Anna" };

            Hl7Message orm = MessageBuilder.Orm(order, Patient(), provider, Clinic, Lab, "SMG000777");
            Hl7Message parsed = Hl7Message.Parse(orm.ToString());

            Assert.Equal("ORM^O01", parsed.TypeAndTrigger);
            Assert.Equal("CRLAB", parsed.Get("MSH-5"));
            Assert.Equal("NW", parsed.Get("ORC-1"));
            Assert.Equal("LO500123", parsed.Get("ORC-2"));
            Assert.Equal("A1C", parsed.Get("OBR-4-1"));
            Assert.Equal("1045230-1", parsed.Get("PID-3-1"));
            Assert.Equal("SMG", parsed.Get("PID-3-4"));
            Assert.Equal("12 Pine & Elm", parsed.Encoding.Unescape(parsed.Get("PID-11-1")));
        }

        [Fact]
        public void ResultsAreGroupedByOrder()
        {
            string oru =
                "MSH|^~\\&|CRLAB|CASCADE|SMGIE|SMG|201303121530||ORU^R01|CRL1|P|2.3\r" +
                "PID|1||1045230-1^^^SMG^MR||O'BRIEN^SEAN||19681130|M\r" +
                "OBR|1|LO500123|CRL900001|A1C^Hemoglobin A1c^L|||201303121500|||||||||1234567893^BRENNAN|||||||||F\r" +
                "OBX|1|NM|4548-4^Hemoglobin A1c^LN||6.4|%|4.0-5.6|H|||F|||201303121500\r" +
                "OBR|2|LO500124|CRL900002|TSH^Thyroid stimulating hormone^L|||201303121500|||||||||1234567893^BRENNAN|||||||||F\r" +
                "OBX|1|NM|3016-3^TSH^LN||2.15|uIU/mL|0.40-4.00|N|||F|||201303121500\r" +
                "OBX|2|TX|||see note||||||F\r";

            List<Hl7Result> results = MessageReader.ReadResults(Hl7Message.Parse(oru));

            Assert.Equal(2, results.Count);
            Assert.Equal("LO500123", results[0].PlacerOrderNumber);
            Assert.Equal("F", results[0].ResultStatus);
            Hl7Observation a1c = Assert.Single(results[0].Observations);
            Assert.Equal("6.4", a1c.Value);
            Assert.Equal("H", a1c.AbnormalFlag);
            Assert.Equal(new DateTime(2013, 3, 12, 15, 0, 0), a1c.ObservedAt);
            Assert.Equal(2, results[1].Observations.Count);
            Assert.Equal("1045230-1", results[1].Patient.Mrn);
        }

        [Fact]
        public void ControlIdsAreUnique()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 500; i++)
            {
                Assert.True(seen.Add(ControlIdGenerator.Next("SMG")));
            }
        }
    }
}
