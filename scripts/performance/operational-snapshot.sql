-- Operator-only, read-only snapshot. Never expose this privileged query as a tenant API.
SET NOCOUNT ON;
DECLARE @observedAt datetimeoffset = SYSDATETIMEOFFSET();
CREATE TABLE #snapshot (tenantId uniqueidentifier, syncsLast24Hours bigint, failedRunsLast24Hours bigint,
  pendingRuns bigint, runningRuns bigint, lastSuccessfulSyncAt datetimeoffset,
  lastSuccessfulWarehouseLoadAt datetimeoffset, warehouseAgeSeconds bigint);
DECLARE @tenantId uniqueidentifier;
DECLARE tenants CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM tenant.Tenants;
OPEN tenants;
FETCH NEXT FROM tenants INTO @tenantId;
WHILE @@FETCH_STATUS=0
BEGIN
  EXEC sys.sp_set_session_context @key=N'TenantId', @value=@tenantId;
  INSERT #snapshot SELECT t.Id AS tenantId,
    (SELECT COUNT_BIG(*) FROM connector.SyncRuns r WHERE r.TenantId=t.Id AND r.CreatedAt>=DATEADD(day,-1,@observedAt)) AS syncsLast24Hours,
    (SELECT COUNT_BIG(*) FROM connector.SyncRuns r WHERE r.TenantId=t.Id AND r.Status='Failed' AND r.CreatedAt>=DATEADD(day,-1,@observedAt)) AS failedRunsLast24Hours,
    (SELECT COUNT_BIG(*) FROM connector.SyncRuns r WHERE r.TenantId=t.Id AND r.Status='Pending') AS pendingRuns,
    (SELECT COUNT_BIG(*) FROM connector.SyncRuns r WHERE r.TenantId=t.Id AND r.Status='Running') AS runningRuns,
    (SELECT MAX(r.FinishedAt) FROM connector.SyncRuns r WHERE r.TenantId=t.Id AND r.Status='Succeeded') AS lastSuccessfulSyncAt,
    (SELECT MAX(b.FinishedAt) FROM warehouse.LoadBatches b WHERE b.TenantId=t.Id AND b.Status='Succeeded') AS lastSuccessfulWarehouseLoadAt,
    DATEDIFF_BIG(second,(SELECT MAX(b.FinishedAt) FROM warehouse.LoadBatches b WHERE b.TenantId=t.Id AND b.Status='Succeeded'),@observedAt) AS warehouseAgeSeconds
    FROM tenant.Tenants t WHERE t.Id=@tenantId;
  FETCH NEXT FROM tenants INTO @tenantId;
END;
CLOSE tenants;
DEALLOCATE tenants;
EXEC sys.sp_set_session_context @key=N'TenantId', @value=NULL;
SELECT @observedAt AS observedAtUtc,
  JSON_QUERY((SELECT * FROM #snapshot ORDER BY tenantId FOR JSON PATH, INCLUDE_NULL_VALUES)) AS tenants
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
