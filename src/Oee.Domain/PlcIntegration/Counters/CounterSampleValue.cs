namespace Oee.Domain.PlcIntegration;

/// <summary>Một mẫu counter đã normalize từ OPC UA, chưa phải ProductionOutput.</summary>
public sealed record CounterSampleValue(decimal Total, decimal Defect, DateTimeOffset SourceTimestamp, string SampleKey);
