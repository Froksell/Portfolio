namespace ClimateRpiGateway.Models;

public record DeviceData
(
    string DeviceId,
    decimal Value,
    string Unit
);