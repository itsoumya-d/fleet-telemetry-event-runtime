namespace FleetTelemetry.Models;

public record TelemetryValidationResult(
    bool IsValid,
    string? ErrorReason,
    IReadOnlyList<AnomalyAlert> Anomalies
);
