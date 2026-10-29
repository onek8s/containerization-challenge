using System.Reflection;
using Proseware.ControlTower.LabTools;

// Lab and test environment tools. Every ILabCommand in this assembly is a command:
//   ControlTower.LabTools <command> [options]

var commands = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => typeof(ILabCommand).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is not null)
    .Select(t => (ILabCommand)Activator.CreateInstance(t)!)
    .OrderBy(c => c.Name, StringComparer.Ordinal)
    .ToList();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help" or "/?")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var command = commands.FirstOrDefault(c => string.Equals(c.Name, args[0], StringComparison.OrdinalIgnoreCase));
if (command is null)
{
    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
    PrintUsage();
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    if (cts.IsCancellationRequested) return;
    e.Cancel = true;
    Console.Error.WriteLine("Stopping...");
    cts.Cancel();
};

try
{
    return await command.RunAsync(args.Skip(1).ToArray(), cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    return 0;
}
catch (LabArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine($"Usage: ControlTower.LabTools {command.Name} {command.Usage}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{command.Name} failed: {ex}");
    return 3;
}

void PrintUsage()
{
    Console.WriteLine("Proseware Control Tower lab tools");
    Console.WriteLine();
    Console.WriteLine("Usage: ControlTower.LabTools <command> [options]");
    Console.WriteLine();
    foreach (var c in commands)
        Console.WriteLine($"  {c.Name,-16} {c.Usage}");
}
