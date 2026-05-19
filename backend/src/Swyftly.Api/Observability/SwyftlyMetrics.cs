using System.Diagnostics.Metrics;

namespace Swyftly.Api.Observability;

public sealed class SwyftlyMetrics : IDisposable
{
    public const string MeterName = "Swyftly.Api";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _paymentEvents;
    private readonly Counter<long> _aiRequests;
    private readonly Counter<long> _orders;
    private readonly Counter<long> _errors;

    public SwyftlyMetrics()
    {
        _paymentEvents = _meter.CreateCounter<long>("swyftly.payments.events");
        _aiRequests = _meter.CreateCounter<long>("swyftly.ai.requests");
        _orders = _meter.CreateCounter<long>("swyftly.orders.created");
        _errors = _meter.CreateCounter<long>("swyftly.errors");
    }

    public void RecordPaymentEvent() => _paymentEvents.Add(1);

    public void RecordAiRequest() => _aiRequests.Add(1);

    public void RecordOrderCreated() => _orders.Add(1);

    public void RecordError() => _errors.Add(1);

    public void Dispose() => _meter.Dispose();
}
