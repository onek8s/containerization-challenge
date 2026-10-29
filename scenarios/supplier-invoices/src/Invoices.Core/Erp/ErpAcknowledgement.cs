using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Wingtip.Invoices.Core.Erp
{
    public class ErpAcknowledgement
    {
        public long InvoiceId { get; set; }
        public bool Accepted { get; set; }
        public string VoucherNumber { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Reads the APACK file written by the ERP after an import run:
        /// APA;invoice id;OK|ER;voucher number;message
        /// </summary>
        public static IList<ErpAcknowledgement> Parse(TextReader reader)
        {
            var list = new List<ErpAcknowledgement>();
            string line;
            var n = 0;
            while ((line = reader.ReadLine()) != null)
            {
                n++;
                if (line.Trim().Length == 0) continue;
                var f = line.Split(';');
                if (f[0] == "APS") continue; // run summary
                if (f[0] != "APA" || f.Length < 4)
                    throw new FormatException("Acknowledgement line " + n + " is not valid: " + line);

                long id;
                if (!long.TryParse(f[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    throw new FormatException("Acknowledgement line " + n + " has an invalid invoice id.");

                list.Add(new ErpAcknowledgement
                {
                    InvoiceId = id,
                    Accepted = f[2].Trim() == "OK",
                    VoucherNumber = f[3].Trim().Length == 0 ? null : f[3].Trim(),
                    Message = f.Length > 4 ? string.Join(";", f, 4, f.Length - 4).Trim() : null
                });
            }
            return list;
        }
    }
}
