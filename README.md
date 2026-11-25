# fleet-telemetry-event-runtime

Event-driven C# .NET 8 stream processor for high-throughput vehicle telematics ingestion, spatial boundary enforcement, and real-time operational anomaly detection.

## Architecture

- **Ingestion & Validation**: Validates schema contracts, timestamp monotonic ordering, and geospatial coordinates for incoming telematics events.
- **Anomaly Detection**: Evaluates multi-factor rules including vehicle overspeed thresholds, harsh deceleration (braking events), and abrupt fuel drop / siphon patterns.
- **Observability**: Thread-safe in-memory metrics collector tracking event processing volume, anomaly alert distributions, and end-to-end processing latency.

## Verification

```bash
dotnet test --verbosity normal
```
