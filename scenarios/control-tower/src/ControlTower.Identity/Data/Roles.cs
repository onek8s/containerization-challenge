namespace Proseware.ControlTower.Identity.Data;

public static class Roles
{
    public const string Admin = "admin";
    public const string Operations = "ops";
    public const string Booking = "booking";
    public const string Customer = "customer";

    public static readonly string[] All = { Admin, Operations, Booking, Customer };

    public static readonly string[] Staff = { Admin, Operations, Booking };
}
