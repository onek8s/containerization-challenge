namespace Wingtip.FieldSales.Core
{
    /// <summary>Order status codes. The numeric values are stored in dbo.SalesOrder.Status.</summary>
    public enum OrderStatus
    {
        Confirmed = 1,
        PendingApproval = 2,
        Rejected = 3,
        Exported = 4,
        Cancelled = 9
    }
}
