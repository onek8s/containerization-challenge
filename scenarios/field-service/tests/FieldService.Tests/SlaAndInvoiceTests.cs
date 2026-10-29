using System.Globalization;
using FieldService.Data.Entities;
using FieldService.Data.Services;

namespace FieldService.Tests;

public class SlaAndInvoiceTests
{
    [Fact]
    public void Emergency_is_due_in_four_hours()
    {
        var created = new DateTime(2024, 3, 8, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(created.AddHours(4), SlaCalculator.DueDate(created, 48, WorkOrderPriority.Emergency));
    }

    [Fact]
    public void Normal_priority_skips_weekend()
    {
        // Friday 12:00 + 24 business hours lands on Monday 12:00.
        var created = new DateTime(2024, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2024, 3, 11, 12, 0, 0, DateTimeKind.Utc), SlaCalculator.DueDate(created, 24, WorkOrderPriority.Normal));
    }

    [Fact]
    public void High_priority_is_capped_at_one_day()
    {
        var created = new DateTime(2024, 3, 6, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(created.AddHours(24), SlaCalculator.DueDate(created, 72, WorkOrderPriority.High));
        Assert.Equal(created.AddHours(8), SlaCalculator.DueDate(created, 8, WorkOrderPriority.High));
    }

    [Fact]
    public void Number_format_is_padded()
    {
        Assert.Equal("WO-2024-00042", NumberSequence.Format("WO", 2024, 42, 5));
    }

    [Fact]
    public void Invoice_file_has_labor_and_part_lines()
    {
        var customer = new Customer { AccountCode = "ACME", HourlyRate = 85m };
        var order = new WorkOrder
        {
            Number = "WO-2024-00001",
            Customer = customer,
            CustomerReference = "PO-7",
            LaborHours = 1.5m,
            CompletedUtc = new DateTime(2024, 5, 2, 10, 0, 0, DateTimeKind.Utc),
            Parts =
            {
                new WorkOrderPart { PartNumber = "VAL-15", Quantity = 2, UnitPrice = 12.40m },
                new WorkOrderPart { PartNumber = "SEAL-3", Quantity = 1, UnitPrice = 3.05m }
            }
        };

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fi-FI");
        try
        {
            var writer = new StringWriter();
            var total = InvoiceExport.Write(writer, "INV-2024-0001", new[] { order });
            var lines = writer.ToString().Trim().Split(Environment.NewLine);

            Assert.Equal(155.35m, total);
            Assert.Equal(InvoiceExport.OrderTotal(order), total);
            Assert.Equal(4, lines.Length);
            Assert.Equal("INV-2024-0001;ACME;WO-2024-00001;PO-7;2024-05-02;LABOR;LABOR;1.50;85.00;127.50", lines[1]);
            Assert.Equal("INV-2024-0001;ACME;WO-2024-00001;PO-7;2024-05-02;PART;VAL-15;2;12.40;24.80", lines[2]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Password_hash_round_trips()
    {
        var hash = PasswordHasher.Hash("s3cret");
        Assert.True(PasswordHasher.Verify("s3cret", hash));
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }
}
