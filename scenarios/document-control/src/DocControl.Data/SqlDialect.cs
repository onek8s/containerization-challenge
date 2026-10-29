namespace Litware.DocControl.Data;

/// <summary>The few statements that differ between SQL Server and SQLite.</summary>
public abstract class SqlDialect
{
    public abstract string InsertReturningId(string insertSql);
    public abstract string Top(int count);
    public abstract string Limit(int count);
    public abstract string NextCounterValue { get; }
    public abstract string ClaimTransmittal { get; }

    public static readonly SqlDialect SqlServer = new SqlServerDialect();
    public static readonly SqlDialect Sqlite = new SqliteDialect();

    private sealed class SqlServerDialect : SqlDialect
    {
        public override string InsertReturningId(string insertSql) => insertSql + "; SELECT CAST(SCOPE_IDENTITY() AS int);";
        public override string Top(int count) => $"TOP ({count})";
        public override string Limit(int count) => "";

        public override string NextCounterValue => """
            MERGE dc.Counters WITH (HOLDLOCK) AS t
            USING (SELECT @Name AS Name) AS s ON t.Name = s.Name
            WHEN MATCHED THEN UPDATE SET Value = t.Value + 1
            WHEN NOT MATCHED THEN INSERT (Name, Value) VALUES (s.Name, 1)
            OUTPUT inserted.Value;
            """;

        public override string ClaimTransmittal => """
            UPDATE TOP (1) t SET Status = 1, ClaimedBy = @Worker, ClaimedUtc = @Now, Attempts = Attempts + 1
            OUTPUT inserted.Id
            FROM dc.Transmittals t WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE t.Status = 0 OR (t.Status = 1 AND t.ClaimedUtc < @StaleBefore);
            """;
    }

    private sealed class SqliteDialect : SqlDialect
    {
        public override string InsertReturningId(string insertSql) => insertSql + "; SELECT last_insert_rowid();";
        public override string Top(int count) => "";
        public override string Limit(int count) => $"LIMIT {count}";

        public override string NextCounterValue => """
            INSERT INTO Counters (Name, Value) VALUES (@Name, 1)
            ON CONFLICT(Name) DO UPDATE SET Value = Value + 1
            RETURNING Value;
            """;

        public override string ClaimTransmittal => """
            UPDATE Transmittals SET Status = 1, ClaimedBy = @Worker, ClaimedUtc = @Now, Attempts = Attempts + 1
            WHERE Id = (SELECT Id FROM Transmittals WHERE Status = 0 OR (Status = 1 AND ClaimedUtc < @StaleBefore) ORDER BY Id LIMIT 1)
            RETURNING Id;
            """;
    }
}
