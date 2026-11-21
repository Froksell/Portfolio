using System.Text.Json;

public class MqttConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Topic { get; set; } = "telemetry/#";
}

public class AppConfig
{
    public MqttConfig Mqtt { get; set; } = new();

    public static AppConfig Load()
    {
        var path = ConfigFinder.Find()
            ?? throw new FileNotFoundException("Hittar inte config.json - kopiera config.example.json och fyll i dina värden.");

        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonSerializerOptions.Web)
               ?? throw new InvalidOperationException("config.json är tom eller ogiltig.");
    }
}

public static class Retry
{
    // Väntar och returnerar false om programmet avbröts under tiden
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