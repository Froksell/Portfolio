using System.Text.Json;

public class AzureSettings
{
    public string EventHubConnectionString { get; set; } = "";
    public string EventHubName { get; set; } = "";
    public string ConsumerGroup { get; set; } = "$Default";

    private class Root
    {
        public AzureSettings Azure { get; set; } = new();
    }

    public static AzureSettings Load()
    {
        var path = ConfigFinder.Find()
            ?? throw new FileNotFoundException("Hittar inte config.json - kopiera config.example.json och fyll i dina värden.");

        var root = JsonSerializer.Deserialize<Root>(File.ReadAllText(path), JsonSerializerOptions.Web)
                   ?? throw new InvalidOperationException("config.json är tom eller ogiltig.");
        return root.Azure;
    }
}

public static class Retry
{
    public static async Task<bool> WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

public static class ConfigFinder
{
    // Söker efter config.json i arbetsmappen och programmets mapp, och uppåt i mapparna
    public static string? Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (var i = 0; dir != null && i < 6; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "config.json");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}