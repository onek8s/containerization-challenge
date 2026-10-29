namespace PolicyChanges.Data.Entities;

/// <summary>Broker or agency that submits changes through the API.</summary>
public class Partner
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ApiKeyHash { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Desk station: a workstation session that enters changes in PolicyDesk.</summary>
public class DeskStation
{
    public string Name { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? ClientVersion { get; set; }
    public string State { get; set; } = "";
    public string? Detail { get; set; }
    public string? CurrentReference { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
}
