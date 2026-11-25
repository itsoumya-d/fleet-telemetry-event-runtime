namespace FleetTelemetry.Models;

public enum AnomalySeverity
{
    Info,
    Warning,
    Critical
}

public record AnomalyAlert(
    string AlertId,
    string VehicleId,
    string AnomalyType,
    AnomalySeverity Severity,
    string Message,
    DateTimeOffset DetectedAt
);
