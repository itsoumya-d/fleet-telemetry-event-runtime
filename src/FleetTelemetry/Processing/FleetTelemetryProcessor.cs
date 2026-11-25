using System.Security.Cryptography;
using System.Text;
using FleetTelemetry.Models;
using FleetTelemetry.Telemetry;

namespace FleetTelemetry.Processing;

public class FleetTelemetryProcessor
{
    private readonly TelemetryMetricsCollector _metrics;
    private readonly HashSet<string> _seenEventSignatures = new();
    private readonly object _lock = new();

    public const double MaxAllowedSpeedKph = 110.0;
    public const double MinFuelPercent = 0.0;
    public const double MaxFuelPercent = 100.0;

    public FleetTelemetryProcessor(TelemetryMetricsCollector? metrics = null)
    {
        _metrics = metrics ?? new TelemetryMetricsCollector();
    }

    public TelemetryMetricsCollector Metrics => _metrics;

    public TelemetryValidationResult ProcessEvent(
        VehicleTelemetryEvent telemetry,
        VehicleTelemetryEvent? previousTelemetry = null)
    {
        if (string.IsNullOrWhiteSpace(telemetry.EventId) || string.IsNullOrWhiteSpace(telemetry.VehicleId))
        {
            _metrics.RecordValidationFailure();
            return new TelemetryValidationResult(false, "EventId and VehicleId must not be empty.", Array.Empty<AnomalyAlert>());
        }

        if (telemetry.Latitude < -90.0 || telemetry.Latitude > 90.0 ||
            telemetry.Longitude < -180.0 || telemetry.Longitude > 180.0)
        {
            _metrics.RecordValidationFailure();
            return new TelemetryValidationResult(false, "Invalid geospatial coordinate bounds.", Array.Empty<AnomalyAlert>());
        }

        if (telemetry.FuelLevelPercent < MinFuelPercent || telemetry.FuelLevelPercent > MaxFuelPercent)
        {
            _metrics.RecordValidationFailure();
            return new TelemetryValidationResult(false, "Fuel level percentage must be between 0 and 100.", Array.Empty<AnomalyAlert>());
        }

        var signature = ComputeEventSignature(telemetry);
        lock (_lock)
        {
            if (!_seenEventSignatures.Add(signature))
            {
                _metrics.RecordValidationFailure();
                return new TelemetryValidationResult(false, "Duplicate telematics event signature detected.", Array.Empty<AnomalyAlert>());
            }
        }

        var anomalies = new List<AnomalyAlert>();

        if (telemetry.SpeedKph > MaxAllowedSpeedKph)
        {
            anomalies.Add(new AnomalyAlert(
                AlertId: Guid.NewGuid().ToString("N"),
                VehicleId: telemetry.VehicleId,
                AnomalyType: "OVER_SPEED_THRESHOLD",
                Severity: AnomalySeverity.Warning,
                Message: $"Vehicle speed of {telemetry.SpeedKph:F1} km/h exceeded statutory threshold {MaxAllowedSpeedKph} km/h.",
                DetectedAt: telemetry.Timestamp
            ));
        }

        if (previousTelemetry != null)
        {
            var deltaSeconds = (telemetry.Timestamp - previousTelemetry.Timestamp).TotalSeconds;
            if (deltaSeconds > 0 && deltaSeconds <= 10.0)
            {
                var speedDelta = previousTelemetry.SpeedKph - telemetry.SpeedKph;
                if (speedDelta > 35.0)
                {
                    anomalies.Add(new AnomalyAlert(
                        AlertId: Guid.NewGuid().ToString("N"),
                        VehicleId: telemetry.VehicleId,
                        AnomalyType: "HARSH_BRAKING_EVENT",
                        Severity: AnomalySeverity.Critical,
                        Message: $"Harsh deceleration detected: drop of {speedDelta:F1} km/h in {deltaSeconds:F1}s.",
                        DetectedAt: telemetry.Timestamp
                    ));
                }

                var fuelDrop = previousTelemetry.FuelLevelPercent - telemetry.FuelLevelPercent;
                if (fuelDrop > 15.0)
                {
                    anomalies.Add(new AnomalyAlert(
                        AlertId: Guid.NewGuid().ToString("N"),
                        VehicleId: telemetry.VehicleId,
                        AnomalyType: "RAPID_FUEL_DROP_ANOMALY",
                        Severity: AnomalySeverity.Critical,
                        Message: $"Rapid fuel level drop of {fuelDrop:F1}% detected in {deltaSeconds:F1}s.",
                        DetectedAt: telemetry.Timestamp
                    ));
                }
            }
        }

        _metrics.RecordProcessedEvent();
        _metrics.RecordAnomalies(anomalies.Count);

        return new TelemetryValidationResult(true, null, anomalies);
    }

    private static string ComputeEventSignature(VehicleTelemetryEvent evt)
    {
        var raw = $"{evt.VehicleId}|{evt.Timestamp.ToUnixTimeMilliseconds()}|{evt.Latitude:F6}|{evt.Longitude:F6}|{evt.OdometerKm:F1}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
