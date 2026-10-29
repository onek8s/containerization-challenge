namespace FieldService.Data.Entities;

public static class Roles
{
    public const string Dispatcher = "Dispatcher";
    public const string Technician = "Technician";
    public const string Admin = "Admin";
}

public class AppUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = Roles.Dispatcher;

    public int? TechnicianId { get; set; }

    public Technician? Technician { get; set; }
}
