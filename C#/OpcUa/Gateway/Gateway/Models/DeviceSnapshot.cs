namespace ClimateRpiGateway.Models;

public record DeviceSnapshot
(
    string DeviceId,
    IReadOnlyDictionary<string, Measurement> Measurements,
    DateTimeOffset LastSeen
);