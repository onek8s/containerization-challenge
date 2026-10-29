using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tailspin.CustomerCare.Domain
{
    public class CustomerImportRow
    {
        public int LineNumber { get; set; }
        public string AccountNumber { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Segment { get; set; }
        public bool IsPriorityCustomer { get; set; }
    }

    public class CustomerImportError
    {
        public int LineNumber { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return "Line " + LineNumber + ": " + Message;
        }
    }

    public class CustomerImportResult
    {
        public List<CustomerImportRow> Rows { get; } = new List<CustomerImportRow>();
        public List<CustomerImportError> Errors { get; } = new List<CustomerImportError>();
    }

    /// <summary>
    /// Parses the nightly customer extract from the billing system.
    /// Semicolon separated, first line is a header, fields may be quoted with double quotes.
    /// </summary>
    public static class CustomerCsvParser
    {
        public static readonly string[] RequiredColumns = { "account_no", "name", "postal_code", "segment" };
        private static readonly string[] Segments = { "RES", "SME", "LCO" };
        private static readonly Regex AccountPattern = new Regex(@"^\d{10}$");
        private static readonly Regex PostalPattern = new Regex(@"^\d{5}$");
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        public static CustomerImportResult Parse(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var result = new CustomerImportResult();
            var headerLine = reader.ReadLine();
            if (headerLine == null)
            {
                result.Errors.Add(new CustomerImportError { LineNumber = 1, Message = "File is empty." });
                return result;
            }

            var header = SplitLine(headerLine.TrimStart('﻿')).Select(h => h.Trim().ToLowerInvariant()).ToList();
            var missing = RequiredColumns.Where(c => !header.Contains(c)).ToList();
            if (missing.Count > 0)
            {
                result.Errors.Add(new CustomerImportError { LineNumber = 1, Message = "Missing columns: " + string.Join(", ", missing) });
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            string line;
            var lineNo = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (line.Trim().Length == 0) continue;

                List<string> fields;
                try
                {
                    fields = SplitLine(line);
                }
                catch (FormatException ex)
                {
                    result.Errors.Add(new CustomerImportError { LineNumber = lineNo, Message = ex.Message });
                    continue;
                }

                Func<string, string> get = name =>
                {
                    var i = header.IndexOf(name);
                    return i >= 0 && i < fields.Count ? Clean(fields[i]) : null;
                };

                var row = new CustomerImportRow
                {
                    LineNumber = lineNo,
                    AccountNumber = get("account_no"),
                    Name = get("name"),
                    Email = get("email"),
                    Phone = get("phone"),
                    Street = get("street"),
                    PostalCode = get("postal_code"),
                    City = get("city"),
                    Segment = (get("segment") ?? string.Empty).ToUpperInvariant(),
                    IsPriorityCustomer = IsTrue(get("priority"))
                };

                var error = Validate(row);
                if (error == null && !seen.Add(row.AccountNumber))
                {
                    error = "Duplicate account number " + row.AccountNumber + ".";
                }

                if (error != null)
                {
                    result.Errors.Add(new CustomerImportError { LineNumber = lineNo, Message = error });
                }
                else
                {
                    result.Rows.Add(row);
                }
            }

            return result;
        }

        public static string Validate(CustomerImportRow row)
        {
            if (row.AccountNumber == null || !AccountPattern.IsMatch(row.AccountNumber)) return "Account number must be 10 digits.";
            if (!CheckDigitOk(row.AccountNumber)) return "Account number check digit is wrong.";
            if (string.IsNullOrEmpty(row.Name)) return "Name is required.";
            if (row.Name.Length > 200) return "Name is longer than 200 characters.";
            if (row.PostalCode == null || !PostalPattern.IsMatch(row.PostalCode)) return "Postal code must be 5 digits.";
            if (Array.IndexOf(Segments, row.Segment) < 0) return "Unknown segment '" + row.Segment + "'.";
            if (!string.IsNullOrEmpty(row.Email) && !EmailPattern.IsMatch(row.Email)) return "E-mail address is not valid.";
            return null;
        }

        /// <summary>Billing account numbers end with a mod 10 (Luhn) check digit.</summary>
        public static bool CheckDigitOk(string accountNumber)
        {
            var sum = 0;
            var doubleIt = false;
            for (var i = accountNumber.Length - 1; i >= 0; i--)
            {
                var d = accountNumber[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }

                sum += d;
                doubleIt = !doubleIt;
            }

            return sum % 10 == 0;
        }

        public static List<string> SplitLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    quoted = true;
                }
                else if (ch == ';')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (quoted) throw new FormatException("Unterminated quoted field.");
            fields.Add(sb.ToString());
            return fields;
        }

        private static string Clean(string value)
        {
            if (value == null) return null;
            value = value.Trim();
            return value.Length == 0 ? null : value;
        }

        private static bool IsTrue(string value)
        {
            return value != null && (value == "1" || value.Equals("Y", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }
    }
}
