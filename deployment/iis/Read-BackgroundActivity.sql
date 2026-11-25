-- Chi doc de quan sat worker, khong gui HTTP va khong thay doi du lieu.
SELECT (SELECT MAX(LastDataAt) FROM plc.PlcDeviceRuntimeStates) AS LastPlcData,
       (SELECT MAX(LastReadAt) FROM plc.CounterCheckpoints) AS LastCounterRead,
       (SELECT MAX(CalculatedAt) FROM oee.OeeSnapshots) AS LastOeeCalculation;

SELECT TOP (30) w.Code AS WorkUnit, m.ExternalDeviceId, r.LastDataAt,
       c.LastReadAt AS CounterSourceAt, c.LastTotalCount, c.LastCounterChangedAt,
       r.ConnectivityState, r.OperationalState,
       CASE WHEN c.LastSampleKey = '!counter-discontinuity' OR c.LastSampleKey LIKE '!cr1|p|%'
            THEN 1 ELSE 0 END AS CounterPending,
       p.Code AS CurrentPlan, p.PlannedStart, p.PlannedEnd,
       COALESCE(o.RecordedTotal, 0) AS RecordedTotal, o.LastOutputAt
FROM plc.PlcDeviceMappings m
JOIN org.WorkUnits w ON w.Id = m.WorkUnitId
LEFT JOIN plc.PlcDeviceRuntimeStates r ON r.PlcDeviceMappingId = m.Id
LEFT JOIN plc.CounterCheckpoints c ON c.PlcDeviceMappingId = m.Id
OUTER APPLY (
    SELECT TOP (1) pp.Id, pp.Code, pp.PlannedStart, pp.PlannedEnd
    FROM planning.ProductionPlans pp
    WHERE pp.WorkUnitId = w.Id AND pp.IsDeleted = 0 AND pp.CancelledAt IS NULL
      AND pp.Status NOT IN (3, 4)
      AND pp.PlannedStart <= SYSDATETIMEOFFSET() AND pp.PlannedEnd > SYSDATETIMEOFFSET()
      AND NOT EXISTS (SELECT 1 FROM planning.ProductionPlans nextPlan WHERE nextPlan.ReplacesPlanId = pp.Id)
    ORDER BY pp.Version DESC, pp.PlannedStart DESC
) p
OUTER APPLY (
    SELECT SUM(po.TotalProducedQuantity) AS RecordedTotal, MAX(po.PeriodEnd) AS LastOutputAt
    FROM production.ProductionOutputs po WHERE po.ProductionPlanId = p.Id
) o
WHERE m.IsDeleted = 0 AND m.IsActive = 1
ORDER BY w.Code;

SELECT TOP (30) w.Code AS WorkUnit, p.Code AS AssignedPlan, o.Id AS OutputId,
       p.PlannedStart, o.PeriodStart, o.PeriodEnd,
       o.TotalProducedQuantity, o.DefectQuantity
FROM production.ProductionOutputs o
JOIN planning.ProductionPlans p ON p.Id = o.ProductionPlanId
JOIN org.WorkUnits w ON w.Id = o.WorkUnitId
WHERE o.SourceNote LIKE 'PLC:%' AND o.PeriodStart < p.PlannedStart
  AND o.PeriodEnd > p.PlannedStart AND DATEDIFF_BIG(SECOND, o.PeriodStart, o.PeriodEnd) > 120
ORDER BY o.PeriodEnd DESC;
