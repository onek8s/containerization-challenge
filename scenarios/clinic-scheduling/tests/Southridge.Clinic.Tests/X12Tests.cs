using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.Tests
{
    [TestClass]
    public class X12Tests
    {
        private static readonly SubmitterInfo Submitter = new SubmitterInfo
        {
            SenderId = "SMGCLINIC",
            ReceiverId = "CLEARHOUSE",
            SubmitterName = "SOUTHRIDGE MEDICAL GROUP",
            ContactName = "BILLING OFFICE",
            ContactPhone = "2535550100",
            ReceiverName = "CASCADE CLAIMS EXCHANGE",
            BillingProviderName = "SOUTHRIDGE MEDICAL GROUP"
        };

        private static string WriteSample(out List<ClaimData> claims)
        {
            DateTime dos = new DateTime(2013, 3, 12);
            claims = new List<ClaimData> { BillingTests.SampleClaim("C200100", dos), BillingTests.SampleClaim("C200101", dos) };
            claims[1].Relationship = (byte)Southridge.Clinic.Domain.InsuredRelationship.Spouse;
            claims[1].SubscriberFirstName = "Erik";
            claims[1].SubscriberLastName = "Lindqvist";
            claims[1].Lines[0].Modifier1 = "25";
            return new Claim837Writer(Submitter).Write(claims, 1042, new DateTime(2013, 3, 13, 22, 15, 0));
        }

        [TestMethod]
        public void InterchangeHeaderHasFixedWidths()
        {
            List<ClaimData> claims;
            string file = WriteSample(out claims);

            Assert.AreEqual('*', file[3]);
            Assert.AreEqual(':', file[104]);
            Assert.AreEqual('~', file[105]);
            StringAssert.Contains(file.Substring(0, 106), "*000001042*1*T*:");
        }

        [TestMethod]
        public void ClaimFileReadsBackSegmentBySegment()
        {
            List<ClaimData> claims;
            var reader = new X12Reader(WriteSample(out claims));
            IList<string[]> segments = reader.Segments;

            string[] gs = segments.Single(s => s[0] == "GS");
            Assert.AreEqual("1042", gs[6]);
            Assert.AreEqual("005010X222A1", gs[8]);

            int st = segments.ToList().FindIndex(s => s[0] == "ST");
            int se = segments.ToList().FindIndex(s => s[0] == "SE");
            Assert.AreEqual((se - st + 1).ToString(), segments[se][1], "SE01 counts ST through SE");

            List<string[]> clm = segments.Where(s => s[0] == "CLM").ToList();
            Assert.AreEqual(2, clm.Count);
            Assert.AreEqual("C200100", clm[0][1]);
            Assert.AreEqual("155", clm[0][2]);
            Assert.AreEqual("11:B:1", clm[0][5]);

            Assert.AreEqual("BK:25000", segments.First(s => s[0] == "HI")[1]);
            Assert.IsTrue(segments.Any(s => s[0] == "SV1" && s[1] == "HC:99213:25"));
            Assert.AreEqual(1, segments.Count(s => s[0] == "PAT"), "patient loop only for the dependent");
        }

        [TestMethod]
        public void RemittanceIsReadAtClaimLevel()
        {
            const string remit =
                "ISA*00*          *00*          *ZZ*CLEARHOUSE     *ZZ*SMGCLINIC      *130320*0800*^*00501*000700001*0*T*:~" +
                "GS*HP*CLEARHOUSE*SMGCLINIC*20130320*0800*700001*X*005010X221A1~ST*835*0001*005010X221A1~" +
                "BPR*I*109*C*ACH*CCP*01*125000024*DA*0099887766*147570**01*125000105*DA*4455667788*20130320~" +
                "TRN*1*EFT13032047570*147570~N1*PR*PREMERA BLUE CROSS~REF*2U*47570~N1*PE*SOUTHRIDGE MEDICAL GROUP*XX*1588667790~LX*1~" +
                "CLP*C200100*1*155*109*15*12*PCN1~CAS*CO*45*31~CAS*PR*3*15~" +
                "CLP*C200101*4*155*0*0*12*PCN2~CAS*CO*16*155~SE*14*0001~GE*1*700001~IEA*1*000700001~";

            Remittance r = Remittance835Parser.Parse(remit);

            Assert.AreEqual("PREMERA BLUE CROSS", r.PayerName);
            Assert.AreEqual("47570", r.PayerIdentifier);
            Assert.AreEqual(109m, r.TotalPaid);
            Assert.AreEqual(new DateTime(2013, 3, 20), r.PaymentDate);
            Assert.AreEqual("EFT13032047570", r.TraceNumber);
            Assert.AreEqual(2, r.Claims.Count);
            Assert.AreEqual(31m, r.Claims[0].PayerAdjustments);
            Assert.AreEqual(15m, r.Claims[0].PatientResponsibility);
            Assert.IsFalse(r.Claims[0].IsDenied);
            Assert.IsTrue(r.Claims[1].IsDenied);
            Assert.AreEqual("PCN2", r.Claims[1].PayerClaimNumber);
        }

        [TestMethod]
        public void ImplementationAcknowledgment()
        {
            const string ack =
                "ISA*00*          *00*          *ZZ*CLEARHOUSE     *ZZ*SMGCLINIC      *130314*0600*^*00501*000700002*0*T*:~\n" +
                "GS*FA*CLEARHOUSE*SMGCLINIC*20130314*0600*700002*X*005010X231A1~\nST*999*0001*005010X231A1~\nAK1*HC*1042*005010X222A1~\n" +
                "AK2*837*0001*005010X222A1~\nIK3*NM1*12**8~\nIK4*9*67*7*~\nIK5*R*5~\nAK9*R*1*1*0~\nSE*8*0001~\nGE*1*700002~\nIEA*1*000700002~\n";

            ImplementationAck result = Ack999Parser.Parse(ack);

            Assert.AreEqual(1042, result.GroupControlNumber);
            Assert.AreEqual("R", result.Status);
            Assert.AreEqual(0, result.AcceptedTransactions);
            Assert.AreEqual(2, result.Errors.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void RejectsNonX12Content()
        {
            new X12Reader("<html>not a claim file</html>");
        }
    }
}
