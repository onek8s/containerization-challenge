using System.Runtime.InteropServices;
using FreightRates.RatingHost.Interop;

namespace FreightRates.Tests;

/// <summary>Struct sizes from mrsrate.h (pack 1).</summary>
public class NativeLayoutTests
{
    [Fact]
    public void Shipment_header_size() => Assert.Equal(41, Marshal.SizeOf<MR_SHIPMENT_HEADER>());

    [Fact]
    public void Line_item_size() => Assert.Equal(8, Marshal.SizeOf<MR_LINE_ITEM>());

    [Fact]
    public void Rate_result_size() => Assert.Equal(7 * 4 + 20 * 8 + 13 + 8 + 8, Marshal.SizeOf<MR_RATE_RESULT>());

    [Fact]
    public void Key_status_size() => Assert.Equal(32, Marshal.SizeOf<MR_KEY_STATUS>());

    [Fact]
    public void Ship_date_offset() =>
        Assert.Equal(20, Marshal.OffsetOf<MR_SHIPMENT_HEADER>(nameof(MR_SHIPMENT_HEADER.ShipDate)).ToInt32());
}
