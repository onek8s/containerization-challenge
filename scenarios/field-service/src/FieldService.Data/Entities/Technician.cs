namespace FieldService.Data.Entities;

public class Technician
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Region { get; set; } = "";

    public string? Phone { get; set; }

    public bool Active { get; set; } = true;
}
