namespace ContosoFoods.TradeRebates.StatementWorker;

public sealed class StatementOptions
{
    public string Root { get; set; } = "";
    public int ReceiveTimeoutSeconds { get; set; } = 5;
    public int MaxMessagesPerRun { get; set; } = 200;
}

public sealed record RunSummary(int Completed, int Failed, int Other);
