namespace ExpenseClaims.Core.Policy;

public sealed record ApprovalStep(int Level, string ApproverEmployeeNumber);

public static class ApprovalRouting
{
    /// <summary>
    /// Builds the approval chain for a claim: the claimant's line manager, and for large claims also
    /// that manager's manager. A step is skipped when it would make the claimant approve their own claim.
    /// </summary>
    /// <param name="managerOf">Returns the manager's employee number, or null at the top of the organisation.</param>
    public static IReadOnlyList<ApprovalStep> Build(
        string claimantEmployeeNumber,
        decimal total,
        ExpensePolicyOptions options,
        Func<string, string?> managerOf,
        string? fallbackApprover = null)
    {
        var steps = new List<ApprovalStep>();
        var levels = total >= options.SecondApprovalFrom ? 2 : 1;
        var current = claimantEmployeeNumber;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { claimantEmployeeNumber };

        while (steps.Count < levels)
        {
            var manager = managerOf(current);
            if (manager is null || !visited.Add(manager))
                break;
            steps.Add(new ApprovalStep(steps.Count + 1, manager));
            current = manager;
        }

        if (steps.Count == 0 && fallbackApprover is not null
            && !string.Equals(fallbackApprover, claimantEmployeeNumber, StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new ApprovalStep(1, fallbackApprover));
        }

        return steps;
    }
}
