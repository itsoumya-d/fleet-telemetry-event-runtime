using FleetTelemetry.Models;
using FleetTelemetry.Processing;
using FleetTelemetry.Telemetry;
using Xunit;

namespace FleetTelemetry.Tests;

public class TelemetryProcessorTests
{
    [Fact]
    public void ProcessEvent_ValidTelemetry_SucceedsWithoutAnomalies()
    {
        var collector = new TelemetryMetricsCollector();
        var processor = new FleetTelemetryProcessor(collector);

        var evt = new VehicleTelemetryEvent(
            EventId: "evt-001",
            VehicleId: "veh-alpha",
            Timestamp: DateTimeOffset.UtcNow,
            Latitude: -36.848461,
            Longitude: 174.763336,
            SpeedKph: 85.0,
            OdometerKm: 12450.0,
            FuelLevelPercent: 78.5,
            EngineStatus: "RUNNING"
        );

        var result = processor.ProcessEvent(evt);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorReason);
        Assert.Empty(result.Anomalies);
        Assert.Equal(1, collector.TotalEventsProcessed);
        Assert.Equal(0, collector.TotalAnomaliesDetected);
    }

    [Fact]
    public void ProcessEvent_InvalidCoordinates_ReturnsValidationError()
    {
        var collector = new TelemetryMetricsCollector();
        var processor = new FleetTelemetryProcessor(collector);

        var evt = new VehicleTelemetryEvent(
            EventId: "evt-002",
            VehicleId: "veh-alpha",
            Timestamp: DateTimeOffset.UtcNow,
            Latitude: 195.0, // Invalid latitude
            Longitude: 174.763336,
            SpeedKph: 50.0,
            OdometerKm: 12450.0,
            FuelLevelPercent: 70.0,
            EngineStatus: "RUNNING"
        );

        var result = processor.ProcessEvent(evt);

        Assert.False(result.IsValid);
        Assert.Contains("geospatial coordinate bounds", result.ErrorReason);
        Assert.Equal(1, collector.TotalValidationFailures);
    }

    [Fact]
    public void ProcessEvent_DuplicateSignature_RejectedAsDuplicate()
    {
        var processor = new FleetTelemetryProcessor();
        var now = DateTimeOffset.UtcNow;

        var evt1 = new VehicleTelemetryEvent("e1", "veh-1", now, -36.8, 174.7, 50.0, 1000.0, 80.0, "RUNNING");
        var evt2 = new VehicleTelemetryEvent("e2", "veh-1", now, -36.8, 174.7, 50.0, 1000.0, 80.0, "RUNNING");

        var res1 = processor.ProcessEvent(evt1);
        var res2 = processor.ProcessEvent(evt2);

        Assert.True(res1.IsValid);
        Assert.False(res2.IsValid);
        Assert.Contains("Duplicate", res2.ErrorReason);
    }

    [Fact]
    public void ProcessEvent_OverSpeedThreshold_EmitsWarningAnomaly()
    {
        var processor = new FleetTelemetryProcessor();
        var evt = new VehicleTelemetryEvent(
            EventId: "evt-speed",
            VehicleId: "veh-beta",
            Timestamp: DateTimeOffset.UtcNow,
            Latitude: -36.848461,
            Longitude: 174.763336,
            SpeedKph: 128.5,
            OdometerKm: 54000.0,
            FuelLevelPercent: 65.0,
            EngineStatus: "RUNNING"
        );

        var result = processor.ProcessEvent(evt);

        Assert.True(result.IsValid);
        Assert.Single(result.Anomalies);
        var anomaly = result.Anomalies[0];
        Assert.Equal("OVER_SPEED_THRESHOLD", anomaly.AnomalyType);
        Assert.Equal(AnomalySeverity.Warning, anomaly.Severity);
    }

    [Fact]
    public void ProcessEvent_HarshBraking_EmitsCriticalAnomaly()
    {
        var processor = new FleetTelemetryProcessor();
        var t0 = DateTimeOffset.UtcNow;

        var prev = new VehicleTelemetryEvent("e-prev", "veh-gamma", t0, -36.85, 174.76, 95.0, 20000.0, 50.0, "RUNNING");
        var curr = new VehicleTelemetryEvent("e-curr", "veh-gamma", t0.AddSeconds(3), -36.8501, 174.7601, 40.0, 20000.1, 50.0, "RUNNING");

        processor.ProcessEvent(prev);
        var result = processor.ProcessEvent(curr, previousTelemetry: prev);

        Assert.True(result.IsValid);
        Assert.Contains(result.Anomalies, a => a.AnomalyType == "HARSH_BRAKING_EVENT");
    }

    [Fact]
    public void ProcessEvent_RapidFuelDrop_EmitsFuelSiphonAnomaly()
    {
        var processor = new FleetTelemetryProcessor();
        var t0 = DateTimeOffset.UtcNow;

        var prev = new VehicleTelemetryEvent("f-prev", "veh-fuel", t0, -36.85, 174.76, 30.0, 20000.0, 85.0, "RUNNING");
        var curr = new VehicleTelemetryEvent("f-curr", "veh-fuel", t0.AddSeconds(5), -36.8501, 174.7601, 30.0, 20000.05, 55.0, "RUNNING");

        processor.ProcessEvent(prev);
        var result = processor.ProcessEvent(curr, previousTelemetry: prev);

        Assert.True(result.IsValid);
        Assert.Contains(result.Anomalies, a => a.AnomalyType == "RAPID_FUEL_DROP_ANOMALY");
    }

    [Fact]
    public void MetricsCollector_Reset_ZeroesAllCounters()
    {
        var collector = new TelemetryMetricsCollector();
        collector.RecordProcessedEvent();
        collector.RecordAnomalies(3);
        collector.RecordValidationFailure();

        Assert.Equal(1, collector.TotalEventsProcessed);
        Assert.Equal(3, collector.TotalAnomaliesDetected);
        Assert.Equal(1, collector.TotalValidationFailures);

        collector.Reset();

        Assert.Equal(0, collector.TotalEventsProcessed);
        Assert.Equal(0, collector.TotalAnomaliesDetected);
        Assert.Equal(0, collector.TotalValidationFailures);
    }
}
