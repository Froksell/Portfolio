// Azure-konsument: läser telemetri från IoT Hubs inbyggda Event Hub-kompatibla
// endpoint och visar inkommande meddelanden i realtid.

using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;

var retryDelay = TimeSpan.FromSeconds(5);

AzureSettings settings;
try
{
    settings = AzureSettings.Load();
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

Console.WriteLine("Lyssnar på Azure IoT Hub (Ctrl+C för att avsluta)");

// Loop: om anslutningen tappas försöker vi igen
while (!ct.IsCancellationRequested)
{
    try
    {
        await using var consumer = string.IsNullOrWhiteSpace(settings.EventHubName)
            ? new EventHubConsumerClient(settings.ConsumerGroup, settings.EventHubConnectionString)
            : new EventHubConsumerClient(settings.ConsumerGroup, settings.EventHubConnectionString, settings.EventHubName);

        // Läser från alla partitioner, endast nya meddelanden
        await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync(
            startReadingAtEarliestEvent: false, cancellationToken: ct))
        {
            if (partitionEvent.Data == null) continue;

            var now = DateTime.Now.ToString("HH:mm:ss");
            var body = partitionEvent.Data.EventBody.ToString();

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var deviceId = root.GetProperty("deviceId").GetString();
                var value = root.GetProperty("value");
                var unit = root.GetProperty("unit").GetString();
                Console.WriteLine($"[{now}] {deviceId,-10} {value} {unit}   (via Azure IoT Hub)");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                Console.WriteLine($"[{now}] Oväntat meddelande: {body}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Azure: fel ({ex.Message}), försöker igen om {retryDelay.TotalSeconds:0} s");
        if (!await Retry.WaitAsync(retryDelay, ct)) break;
    }
}

Console.WriteLine("Avslutar");