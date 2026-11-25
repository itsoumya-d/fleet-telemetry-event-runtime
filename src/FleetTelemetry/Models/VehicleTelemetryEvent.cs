namespace FleetTelemetry.Models;

public record VehicleTelemetryEvent(
    string EventId,
    string VehicleId,
    DateTimeOffset Timestamp,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double OdometerKm,
    double FuelLevelPercent,
    string EngineStatus
);
