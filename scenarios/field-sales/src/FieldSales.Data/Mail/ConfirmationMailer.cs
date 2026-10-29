using System;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Data.Mail
{
    public class MailSettings
    {
        public string From { get; set; }
        public string OfficeCopy { get; set; }

        public static MailSettings FromConfig()
        {
            return new MailSettings
            {
                From = ConfigurationManager.AppSettings["FieldSales:MailFrom"] ?? "orders@wingtipfoods.com",
                OfficeCopy = ConfigurationManager.AppSettings["FieldSales:MailOfficeCopy"]
            };
        }
    }

    /// <summary>
    /// Sends the order confirmation to the store. SMTP host, port and pickup folder come from
    /// system.net/mailSettings in Web.config.
    /// </summary>
    public class ConfirmationMailer
    {
        private static readonly CultureInfo Format = CultureInfo.GetCultureInfo("en-GB");
        private readonly FieldSalesDbContext _db;
        private readonly MailSettings _settings;

        public ConfirmationMailer(FieldSalesDbContext db, MailSettings settings)
        {
            _db = db;
            _settings = settings;
        }

        /// <returns>False when the customer has no e-mail address or the order is not confirmed.</returns>
        public async Task<bool> SendAsync(int salesOrderId)
        {
            var order = await _db.SalesOrders.Include(o => o.Customer).Include(o => o.Lines).SingleAsync(o => o.SalesOrderId == salesOrderId);
            if (order.Status != (int)OrderStatus.Confirmed || string.IsNullOrWhiteSpace(order.Customer.Email) || order.ConfirmationSentAt != null)
                return false;

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_settings.From, "Wingtip Foods Orders");
                message.To.Add(order.Customer.Email);
                if (!string.IsNullOrEmpty(_settings.OfficeCopy)) message.Bcc.Add(_settings.OfficeCopy);
                message.Subject = string.Format("Order confirmation {0} ({1})", order.OrderNo, order.RepReference);
                message.Body = Body(order);
                message.BodyEncoding = Encoding.UTF8;

                using (var smtp = new SmtpClient())
                {
                    await smtp.SendMailAsync(message);
                }
            }

            order.ConfirmationSentAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return true;
        }

        public static string Body(SalesOrderEntity order)
        {
            var text = new StringBuilder();
            text.AppendLine("Thank you for your order.");
            text.AppendLine();
            text.AppendFormat(Format, "Order:      {0}\r\n", order.OrderNo);
            text.AppendFormat(Format, "Reference:  {0}\r\n", order.RepReference);
            text.AppendFormat(Format, "Customer:   {0} {1}\r\n", order.Customer.CustomerNo, order.Customer.Name);
            text.AppendFormat(Format, "Delivery:   {0:dddd d MMMM yyyy}\r\n", order.RequestedDelivery);
            text.AppendLine();
            foreach (var line in order.Lines.OrderBy(l => l.LineNo))
            {
                text.AppendFormat(Format, "{0,-8} {1,-34} {2,5} x {3,9:N2} {4,6} {5,11:N2}\r\n",
                    line.ItemNo, Truncate(line.ItemName, 34), line.Quantity, line.UnitPrice,
                    line.LineDiscountPercent > 0 ? "-" + line.LineDiscountPercent.ToString("0.#", Format) + "%" : "", line.LineTotal);
            }
            text.AppendLine();
            text.AppendFormat(Format, "{0,-64} {1,11:N2}\r\n", "Total excl. VAT", order.NetTotal);
            text.AppendFormat(Format, "{0,-64} {1,11:N2}\r\n", "VAT", order.VatTotal);
            text.AppendFormat(Format, "{0,-64} {1,11:N2}\r\n", "Total EUR", order.NetTotal + order.VatTotal);
            if (!string.IsNullOrWhiteSpace(order.Comment))
            {
                text.AppendLine();
                text.AppendLine("Comment: " + order.Comment);
            }
            return text.ToString();
        }

        private static string Truncate(string value, int length)
        {
            value = value ?? "";
            return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
        }
    }
}
