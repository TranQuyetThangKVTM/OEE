-- READ ONLY. Run in SSMS after selecting the actual OEE database.
-- Change the device code and the local shift boundary below. No production data is modified.
DECLARE @WorkUnitCode nvarchar(32) = N'M11';
DECLARE @Boundary datetimeoffset(7) = '2026-10-31T06:00:00+07:00';
DECLARE @WindowHours int = 12;
DECLARE @DeviceId uniqueidentifier;

SELECT @DeviceId = m.Id
FROM plc.PlcDeviceMappings AS m
JOIN org.WorkUnits AS w ON w.Id = m.WorkUnitId
WHERE w.Code = @WorkUnitCode AND m.IsDeleted = 0 AND w.IsDeleted = 0;

IF @DeviceId IS NULL
BEGIN
    RAISERROR('No PLC mapping found for this WorkUnit code.', 16, 1);
    RETURN;
END;

DECLARE @Prefix nvarchar(200) = N'plc:' + REPLACE(CONVERT(nvarchar(36), @DeviceId), '-', '') + N':%';

;WITH Samples AS
(
    SELECT o.Id,
        COALESCE(TRY_CONVERT(datetimeoffset(7), JSON_VALUE(o.Payload, '$.TotalCounterTimestamp')),
                 TRY_CONVERT(datetimeoffset(7), JSON_VALUE(o.Payload, '$.SourceTimestamp'))) AS CounterSourceAt,
        TRY_CONVERT(datetimeoffset(7), JSON_VALUE(o.Payload, '$.ReceivedAt')) AS ReceivedAt,
        TRY_CONVERT(decimal(28, 4), JSON_VALUE(o.Payload, '$.TotalCounter')) AS TotalCounter,
        TRY_CONVERT(decimal(28, 4), JSON_VALUE(o.Payload, '$.DefectCounter')) AS DefectCounter,
        o.ProcessedAt
    FROM integration.OutboxMessages AS o
    WHERE o.Type = N'PlcTelemetryFrame' AND o.IdempotencyKey LIKE @Prefix
      AND o.OccurredAt >= DATEADD(hour, -@WindowHours, @Boundary)
      AND o.OccurredAt <= DATEADD(hour, @WindowHours, @Boundary)
), Nearest AS
(
    SELECT TOP (40) *
    FROM Samples
    WHERE CounterSourceAt IS NOT NULL
    ORDER BY ABS(DATEDIFF_BIG(millisecond, CounterSourceAt, @Boundary)), Id
)
SELECT @WorkUnitCode AS WorkUnit,
    CASE WHEN CounterSourceAt = @Boundary THEN 'EXACT_SOURCE_TIME'
         WHEN CounterSourceAt < @Boundary THEN 'BEFORE_BOUNDARY'
         ELSE 'AFTER_BOUNDARY' END AS Position,
    CounterSourceAt AT TIME ZONE 'SE Asia Standard Time' AS CounterSourceTimeUTC7,
    ReceivedAt AT TIME ZONE 'SE Asia Standard Time' AS ReceivedTimeUTC7,
    TotalCounter, DefectCounter,
    DATEDIFF_BIG(millisecond, @Boundary, CounterSourceAt) AS MillisecondsFromBoundary,
    ProcessedAt, Id
FROM Nearest
ORDER BY CounterSourceAt, Id;

-- These are samples OEE actually received and committed, not a complete PLC historian.
-- Two nearby samples alone do not prove how production was split inside a missing interval.
