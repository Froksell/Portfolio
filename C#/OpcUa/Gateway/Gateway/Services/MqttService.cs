using ClimateRpiGateway.Configuration;
using ClimateRpiGateway.Models;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Text.Json;

namespace ClimateRpiGateway.Services;

public class MqttService(
    GatewayOptions options,
    AzureService azure,
    ILogger<MqttService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await azure.ConnectAsync(ct);

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += e => HandleMessageAsync(e, ct);

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Mqtt.Host, options.Mqtt.Port)
            .WithClientId(options.Mqtt.ClientId);
        if (!string.IsNullOrEmpty(options.Mqtt.Username))
            builder.WithCredentials(options.Mqtt.Username, options.Mqtt.Password);
        var mqttOptions = builder.Build();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    logger.LogInformation("MQTT: ansluter till {Host}:{Port}...", options.Mqtt.Host, options.Mqtt.Port);
                    await client.ConnectAsync(mqttOptions, ct);

                    await client.SubscribeAsync(
                        factory.CreateSubscribeOptionsBuilder().WithTopicFilter(options.Mqtt.Topic).Build(), ct);
                    logger.LogInformation("MQTT: ansluten, prenumererar på '{Topic}'", options.Mqtt.Topic);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("MQTT: fel ({Message}), försöker igen om {Seconds} s",
                    ex.Message, RetryDelay.TotalSeconds);
                try { await Task.Delay(RetryDelay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        if (client.IsConnected)
            await client.DisconnectAsync();
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e, CancellationToken ct)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();

        if (!TryParse(payload, out var data) || data is null)
        {
            logger.LogWarning("Ogiltigt meddelande på {Topic} (kräver deviceId, value, unit): {Payload}", topic, payload);
            return;
        }

        // Topic har formen telemetry/<deviceId>/<type>; typen är sista delen
        var type = topic.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown";
        var measurement = new Measurement(type, data.Value, data.Unit);

        try
        {
            await azure.SendAsync(data, measurement, topic, ct);
        }
        catch (OperationCanceledException)
        {
          
        }
        catch (Exception ex)
        {
            logger.LogError("Kunde inte skicka till Azure: {Message}", ex.Message);
        }
    }

    private static bool TryParse(string payload, out DeviceData? data)
    {
        data = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("deviceId", out var deviceId) ||
                !root.TryGetProperty("value", out var value) ||
                !root.TryGetProperty("unit", out var unit) ||
                value.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            var id = deviceId.GetString();
            var unitText = unit.GetString();
            if (string.IsNullOrEmpty(id) || unitText is null)
                return false;

            data = new DeviceData(id, value.GetDecimal(), unitText);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}