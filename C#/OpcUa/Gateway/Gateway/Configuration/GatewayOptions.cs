namespace ClimateRpiGateway.Configuration;

public class GatewayOptions
{
    public MqttOptions Mqtt { get; set; } = new();
    public AzureOptions Azure { get; set; } = new();
}

public class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Topic { get; set; } = "telemetry/#";
    public string ClientId { get; set; } = "pi-gateway";
}

public class AzureOptions
{
    public string DeviceConnectionString { get; set; } = "";
}