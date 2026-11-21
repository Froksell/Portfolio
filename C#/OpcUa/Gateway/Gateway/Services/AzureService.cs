using ClimateRpiGateway.Configuration;
using ClimateRpiGateway.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ClimateRpiGateway.Services;

// Skickar telemetri till Azure IoT Hub som gatewayens egen enhet
public sealed class AzureService(GatewayOptions options, ILogger<AzureService> logger) : IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly DeviceClient _client =
        DeviceClient.CreateFromConnectionString(options.Azure.DeviceConnectionString, TransportType.Mqtt);

    // Försöker ansluta tills det lyckas eller programmet stoppas
    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _client.OpenAsync(ct);
                logger.LogInformation("Ansluten till Azure IoT Hub");
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Azure: kunde inte ansluta ({Message}), försöker igen om {Seconds} s",
                    ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    public async Task SendAsync(DeviceData data, Measurement measurement, string topic, CancellationToken ct)
    {
        // deviceId, value och unit från ESP32 finns kvar i meddelandet
        var json = JsonSerializer.Serialize(new
        {
            deviceId = data.DeviceId,
            type = measurement.Type,
            value = data.Value,
            unit = data.Unit,
            topic,
            gatewayTimestamp = DateTimeOffset.UtcNow
        }, JsonSerializerOptions.Web);

        using var message = new Message(Encoding.UTF8.GetBytes(json))
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };
        message.Properties["deviceId"] = data.DeviceId;
        message.Properties["type"] = measurement.Type;

        await _client.SendEventAsync(message, ct);
        logger.LogInformation("-> Azure: {DeviceId} {Type} {Value} {Unit}",
            data.DeviceId, measurement.Type, data.Value, data.Unit);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.CloseAsync();
        _client.Dispose();
    }
}