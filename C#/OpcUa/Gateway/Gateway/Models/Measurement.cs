
namespace ClimateRpiGateway.Models;

public record Measurement
(
    string Type,
    decimal Value,
    string Unit
);

public record GatewayData
(
    string GatewayId,
    DateTimeOffset SentAt,
    DeviceSnapshot[] Devices
);