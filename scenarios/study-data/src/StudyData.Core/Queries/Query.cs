namespace StudyData.Core.Queries;

public enum QueryState
{
    Open,
    Answered,
    Closed,
    Cancelled
}

public enum QueryOrigin
{
    EditCheck,
    Monitor,
    DataManager,
    LabImport,
    Coding
}

public class Query
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int FormInstanceId { get; set; }
    public string FieldCode { get; set; } = "";
    public QueryOrigin Origin { get; set; }
    public string? EditCheckCode { get; set; }
    public QueryState State { get; set; }
    public string Text { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public string OpenedBy { get; set; } = "";
    public DateTime? LastReminderAt { get; set; }
    public int ReminderCount { get; set; }

    public List<QueryMessage> Messages { get; set; } = new();
}

public class QueryMessage
{
    public int Id { get; set; }
    public int QueryId { get; set; }
    public DateTime At { get; set; }
    public string User { get; set; } = "";
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
    public QueryState NewState { get; set; }
}

public class QueryTransitionException : InvalidOperationException
{
    public QueryTransitionException(string message) : base(message)
    {
    }
}

/// <summary>
/// Query lifecycle: sites answer, monitors and data managers close or re-open.
/// Automatic queries from edit checks close themselves when the data is corrected.
/// </summary>
public static class QueryWorkflow
{
    public static Query Open(int subjectId, int formInstanceId, string fieldCode, QueryOrigin origin,
        string text, string user, DateTime now, string? editCheckCode = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new QueryTransitionException("Query text is required.");
        }

        var q = new Query
        {
            SubjectId = subjectId,
            FormInstanceId = formInstanceId,
            FieldCode = fieldCode,
            Origin = origin,
            EditCheckCode = editCheckCode,
            State = QueryState.Open,
            Text = text.Trim(),
            OpenedAt = now,
            OpenedBy = user
        };
        q.Messages.Add(new QueryMessage { At = now, User = user, Role = origin.ToString(), Text = q.Text, NewState = QueryState.Open });
        return q;
    }

    public static void Answer(Query q, string text, string user, DateTime now)
    {
        if (q.State != QueryState.Open)
        {
            throw new QueryTransitionException($"Query {q.Id} is {q.State} and cannot be answered.");
        }

        Require(text, "An answer is required.");
        q.State = QueryState.Answered;
        q.Messages.Add(new QueryMessage { At = now, User = user, Role = "Site", Text = text.Trim(), NewState = QueryState.Answered });
    }

    public static void Close(Query q, string? text, string user, DateTime now)
    {
        if (q.State is QueryState.Closed or QueryState.Cancelled)
        {
            throw new QueryTransitionException($"Query {q.Id} is already {q.State}.");
        }

        if (q.State == QueryState.Open && q.Origin != QueryOrigin.EditCheck && string.IsNullOrWhiteSpace(text))
        {
            throw new QueryTransitionException("Closing an unanswered query needs a comment.");
        }

        q.State = QueryState.Closed;
        q.Messages.Add(new QueryMessage { At = now, User = user, Role = "Review", Text = text?.Trim() ?? "Closed", NewState = QueryState.Closed });
    }

    public static void Reopen(Query q, string text, string user, DateTime now)
    {
        if (q.State != QueryState.Answered)
        {
            throw new QueryTransitionException("Only answered queries can be re-opened.");
        }

        Require(text, "Say why the answer is not accepted.");
        q.State = QueryState.Open;
        q.Messages.Add(new QueryMessage { At = now, User = user, Role = "Review", Text = text.Trim(), NewState = QueryState.Open });
    }

    public static void Cancel(Query q, string text, string user, DateTime now)
    {
        if (q.State is QueryState.Closed or QueryState.Cancelled)
        {
            throw new QueryTransitionException($"Query {q.Id} is already {q.State}.");
        }

        Require(text, "A reason is required.");
        q.State = QueryState.Cancelled;
        q.Messages.Add(new QueryMessage { At = now, User = user, Role = "Review", Text = text.Trim(), NewState = QueryState.Cancelled });
    }

    /// <summary>Called when the data behind an automatic query has been corrected.</summary>
    public static void AutoClose(Query q, DateTime now)
    {
        if (q.Origin == QueryOrigin.EditCheck && q.State is QueryState.Open or QueryState.Answered)
        {
            q.State = QueryState.Closed;
            q.Messages.Add(new QueryMessage { At = now, User = "system", Role = "EditCheck", Text = "Data corrected, check passes.", NewState = QueryState.Closed });
        }
    }

    public static bool BlocksSignature(Query q) => q.State is QueryState.Open or QueryState.Answered;

    public static bool NeedsReminder(Query q, DateTime now, int days) =>
        q.State == QueryState.Open && (now - (q.LastReminderAt ?? q.OpenedAt)).TotalDays >= days;

    private static void Require(string? text, string message)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new QueryTransitionException(message);
        }
    }
}
