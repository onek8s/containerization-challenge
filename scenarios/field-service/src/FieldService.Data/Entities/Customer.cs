namespace FieldService.Data.Entities;

public class Customer
{
    public int Id { get; set; }

    public string AccountCode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public decimal HourlyRate { get; set; }

    public int SlaHours { get; set; } = 48;

    public List<WorkOrder> WorkOrders { get; set; } = new();
}
