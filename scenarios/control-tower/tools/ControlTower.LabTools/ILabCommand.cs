namespace Proseware.ControlTower.LabTools;

/// <summary>A lab tool command, e.g. "ControlTower.LabTools seed --connection ...".</summary>
public interface ILabCommand
{
    string Name { get; }

    string Usage { get; }

    Task<int> RunAsync(string[] args, CancellationToken cancellationToken);
}
