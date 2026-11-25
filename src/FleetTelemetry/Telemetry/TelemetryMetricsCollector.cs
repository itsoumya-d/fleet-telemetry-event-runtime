namespace FleetTelemetry.Telemetry;

public class TelemetryMetricsCollector
{
    private long _totalEventsProcessed;
    private long _totalAnomaliesDetected;
    private long _totalValidationFailures;

    public long TotalEventsProcessed => Interlocked.Read(ref _totalEventsProcessed);
    public long TotalAnomaliesDetected => Interlocked.Read(ref _totalAnomaliesDetected);
    public long TotalValidationFailures => Interlocked.Read(ref _totalValidationFailures);

    public void RecordProcessedEvent() => Interlocked.Increment(ref _totalEventsProcessed);
    public void RecordValidationFailure() => Interlocked.Increment(ref _totalValidationFailures);
    public void RecordAnomalies(int count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _totalAnomaliesDetected, count);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalEventsProcessed, 0);
        Interlocked.Exchange(ref _totalAnomaliesDetected, 0);
        Interlocked.Exchange(ref _totalValidationFailures, 0);
    }
}
