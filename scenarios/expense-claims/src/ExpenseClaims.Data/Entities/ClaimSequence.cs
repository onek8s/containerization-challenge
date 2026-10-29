namespace ExpenseClaims.Data.Entities;

/// <summary>Running claim number per year.</summary>
public class ClaimSequence
{
    public int Year { get; set; }
    public int LastNumber { get; set; }
}
