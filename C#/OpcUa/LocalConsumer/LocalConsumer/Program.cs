// Lokal MQTT-konsument: visar telemetri från båda ESP32-enheterna i realtid.

using System.Text.Json;
using MQTTnet;

var retryDelay = TimeSpan.FromSeconds(5);

AppConfig config;
try
{
    config = AppConfig.Load();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
var ct = cts.Token;

var factory = new MqttClientFactory();
using var mqtt = factory.CreateMqttClient();

mqtt.ApplicationMessageReceivedAsync += e =>
{
    var now = DateTime.Now.ToString("HH:mm:ss");
    var topic = e.ApplicationMessage.Topic;
    var payload = e.ApplicationMessage.ConvertPayloadToString();

    try
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var deviceId = root.GetProperty("deviceId").GetString();
        var value = root.GetProperty("value");
        var unit = root.GetProperty("unit").GetString();
        Console.WriteLine($"[{now}] {deviceId,-10} {value} {unit}   ({topic})");
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
        Console.WriteLine($"[{now}] Oväntat meddelande på {topic}: {payload}");
    }

    return Task.CompletedTask;
};

var builder = new MqttClientOptionsBuilder()
    .WithTcpServer(config.Mqtt.Host, config.Mqtt.Port)
    .WithClientId("local-consumer");
if (!string.IsNullOrEmpty(config.Mqtt.Username))
    builder.WithCredentials(config.Mqtt.Username, config.Mqtt.Password);
var options = builder.Build();

// Loop: anslut (eller återanslut) och prenumerera tills programmet stoppas
while (!ct.IsCancellationRequested)
{
    try
    {
        if (!mqtt.IsConnected)
        {
            Console.WriteLine($"MQTT: ansluter till {config.Mqtt.Host}:{config.Mqtt.Port}...");
            await mqtt.ConnectAsync(options, ct);

            await mqtt.SubscribeAsync(
                factory.CreateSubscribeOptionsBuilder().WithTopicFilter(config.Mqtt.Topic).Build(), ct);
            Console.WriteLine($"MQTT: ansluten, lyssnar på '{config.Mqtt.Topic}' (Ctrl+C för att avsluta)");
        }

        if (!await Retry.WaitAsync(TimeSpan.FromSeconds(1), ct)) break;
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"MQTT: fel ({ex.Message}), försöker igen om {retryDelay.TotalSeconds:0} s");
        if (!await Retry.WaitAsync(retryDelay, ct)) break;
    }
}

if (mqtt.IsConnected) await mqtt.DisconnectAsync();