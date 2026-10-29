using System.Drawing.Drawing2D;

namespace LabelStation;

/// <summary>
/// Draws a shipping label. Coordinates are in millimetres.
/// </summary>
public static class LabelRenderer
{
    public static void Render(Graphics g, ShippingLabel label, SizeF sizeMm, string stationId)
    {
        g.PageUnit = GraphicsUnit.Millimeter;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.White);

        using var border = new Pen(Color.Black, 0.5f);
        using var heading = new Font("Arial", 22, FontStyle.Bold, GraphicsUnit.Point);
        using var body = new Font("Arial", 11, FontStyle.Regular, GraphicsUnit.Point);
        using var small = new Font("Arial", 8, FontStyle.Regular, GraphicsUnit.Point);

        var margin = 4f;
        var width = sizeMm.Width - 2 * margin;
        g.DrawRectangle(border, margin, margin, width, sizeMm.Height - 2 * margin);

        var y = margin + 3;
        g.DrawString(label.Carrier.ToUpperInvariant(), heading, Brushes.Black, margin + 3, y);
        y += 14;
        g.DrawLine(border, margin, y, margin + width, y);

        y += 3;
        g.DrawString("SHIP TO", small, Brushes.Black, margin + 3, y);
        y += 5;
        g.DrawString(label.ShipToName, body, Brushes.Black, margin + 3, y);
        y += 6;
        g.DrawString(label.ShipToAddress, body, Brushes.Black, new RectangleF(margin + 3, y, width - 6, 22));
        y += 24;
        g.DrawLine(border, margin, y, margin + width, y);

        y += 3;
        g.DrawString($"Order {label.OrderNumber}", body, Brushes.Black, margin + 3, y);
        g.DrawString($"Parcel {label.ParcelIndex} / {label.ParcelCount}", body, Brushes.Black, margin + width / 2, y);
        y += 6;
        g.DrawString($"Weight {label.WeightKg:0.0} kg", body, Brushes.Black, margin + 3, y);
        y += 10;

        var barcodeHeight = Math.Min(30f, sizeMm.Height - y - 18);
        Code39.Draw(g, label.TrackingCode, new RectangleF(margin + 6, y, width - 12, barcodeHeight));
        y += barcodeHeight + 2;

        var codeSize = g.MeasureString(label.TrackingCode, body);
        g.DrawString(label.TrackingCode, body, Brushes.Black, margin + (width - codeSize.Width) / 2, y);

        g.DrawString($"{stationId}  {DateTime.Now:yyyy-MM-dd HH:mm}", small, Brushes.Black,
            margin + 3, sizeMm.Height - margin - 5);
    }
}
