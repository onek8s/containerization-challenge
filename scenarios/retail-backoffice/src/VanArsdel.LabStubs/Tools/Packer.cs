namespace VanArsdel.LabStubs.Tools;

/// <summary>"pack": plays the warehouse and store staff, packing every open pick task.</summary>
public class Packer
{
    private readonly IConfiguration _configuration;

    public Packer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var client = new LabClient(_configuration, "admin");
        var tasks = await client.GetAsync("storeops/tasks?take=200");
        var packed = 0;
        foreach (var task in tasks.EnumerateArray())
        {
            var id = task.GetProperty("id").GetString();
            if (task.GetProperty("status").GetString() == "Open")
            {
                await client.SendJsonAsync(HttpMethod.Post, $"storeops/tasks/{id}/start", null);
            }
            await client.SendJsonAsync(HttpMethod.Post, $"storeops/tasks/{id}/packed", new { parcels = 1, lines = Array.Empty<object>() });
            Console.WriteLine($"Packed {task.GetProperty("orderNo").GetString()} at {task.GetProperty("storeCode").GetString()}");
            packed++;
        }
        if (args.Contains("--collect"))
        {
            var waiting = await client.GetAsync("storeops/collections");
            foreach (var c in waiting.EnumerateArray())
            {
                await client.SendJsonAsync(HttpMethod.Post, $"storeops/collections/{c.GetProperty("id").GetInt64()}/handover", null);
                Console.WriteLine($"Handed over {c.GetProperty("orderNo").GetString()} at {c.GetProperty("storeCode").GetString()}");
            }
        }
        Console.WriteLine($"{packed} tasks packed");
        return 0;
    }
}
