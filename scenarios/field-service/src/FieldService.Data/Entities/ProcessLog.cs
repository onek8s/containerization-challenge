namespace FieldService.Data.Entities;

public class ImportedFile
{
    public int Id { get; set; }

    public string FileName { get; set; } = "";

    public DateTime ProcessedUtc { get; set; }

    public int OrdersCreated { get; set; }

    public int RowsRejected { get; set; }

    public string? Error { get; set; }
}

public class InvoiceBatch
{
    public int Id { get; set; }

    public string BatchNumber { get; set; } = "";

    public DateTime CreatedUtc { get; set; }

    public string FileName { get; set; } = "";

    public int OrderCount { get; set; }

    public decimal TotalAmount { get; set; }
}

public class SequenceCounter
{
    public string Name { get; set; } = "";

    public int Year { get; set; }

    public int Value { get; set; }
}
