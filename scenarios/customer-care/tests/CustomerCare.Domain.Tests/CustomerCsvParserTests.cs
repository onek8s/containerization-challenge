using System.IO;
using System.Linq;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class CustomerCsvParserTests
    {
        private const string Header = "account_no;name;email;phone;street;postal_code;city;segment;priority";

        [Fact]
        public void Parses_valid_rows()
        {
            var csv = Header + "\n" +
                      "1234567897;\"Virtanen, Matti\";matti@example.com;0401234567;Kauppakatu 1;00100;Helsinki;res;Y\n" +
                      "\n" +
                      "0000000018;Oy Example Ab;;;;33100;Tampere;SME;\n";
            var r = CustomerCsvParser.Parse(new StringReader(csv));
            Assert.Empty(r.Errors);
            Assert.Equal(2, r.Rows.Count);
            Assert.Equal("Virtanen, Matti", r.Rows[0].Name);
            Assert.Equal("RES", r.Rows[0].Segment);
            Assert.True(r.Rows[0].IsPriorityCustomer);
            Assert.Null(r.Rows[1].Email);
            Assert.Equal(4, r.Rows[1].LineNumber);
        }

        [Fact]
        public void Reports_missing_columns()
        {
            var r = CustomerCsvParser.Parse(new StringReader("account_no;name\n"));
            Assert.Single(r.Errors);
            Assert.Contains("postal_code", r.Errors[0].Message);
        }

        [Fact]
        public void Empty_file()
        {
            Assert.Single(CustomerCsvParser.Parse(new StringReader(string.Empty)).Errors);
        }

        [Theory]
        [InlineData("123;A;;;;00100;X;RES;", "10 digits")]
        [InlineData("1234567890;A;;;;00100;X;RES;", "check digit")]
        [InlineData("1234567897;;;;;00100;X;RES;", "Name")]
        [InlineData("1234567897;A;;;;001;X;RES;", "Postal")]
        [InlineData("1234567897;A;;;;00100;X;GOV;", "segment")]
        [InlineData("1234567897;A;bad@;;;00100;X;RES;", "E-mail")]
        [InlineData("1234567897;\"A;;;;00100;X;RES;", "Unterminated")]
        public void Rejects_invalid_rows(string line, string expected)
        {
            var r = CustomerCsvParser.Parse(new StringReader(Header + "\n" + line));
            Assert.Empty(r.Rows);
            Assert.Contains(expected, r.Errors.Single().Message);
        }

        [Fact]
        public void Rejects_duplicates()
        {
            var csv = Header + "\n1234567897;A;;;;00100;X;RES;\n1234567897;B;;;;00100;X;RES;";
            var r = CustomerCsvParser.Parse(new StringReader(csv));
            Assert.Single(r.Rows);
            Assert.Equal(3, r.Errors.Single().LineNumber);
        }

        [Fact]
        public void Handles_escaped_quotes_and_bom()
        {
            var csv = "﻿" + Header + "\n1234567897;\"The \"\"Best\"\" Shop\";;;;00100;X;SME;0";
            var r = CustomerCsvParser.Parse(new StringReader(csv));
            Assert.Equal("The \"Best\" Shop", r.Rows.Single().Name);
            Assert.False(r.Rows.Single().IsPriorityCustomer);
        }
    }
}
