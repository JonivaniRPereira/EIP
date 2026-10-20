import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync, openSync, closeSync } from "node:fs";
import { join } from "node:path";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { createTransientDemoSource } from "./transient-demo-source.mjs";
import { createVolumeDemoSource } from "./volume-demo-source.mjs";

export async function verifyRestoredIngestion({
  api,
  token,
  tenant,
  base,
  env,
  workerDll,
  root,
  output,
  sql,
  verifyRaw,
  invoke,
  rabbit,
  initialFreshness,
  verifyRetry = false,
  verifyVolume = false,
}) {
  const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  const entities = ["customers", "products", "sales-invoices"];
  const connectors = await api("/api/v1/connectors", undefined, token);
  const selected = entities.map((entity) => {
    const matches = connectors.filter((c) => c.sourceEntity === entity);
    assert.equal(
      matches.length,
      1,
      "Expected one Demo connector for each entity",
    );
    assert(/^[a-f0-9-]{36}$/i.test(matches[0].id));
    return matches[0];
  });
  const counts = () =>
    sql(`SELECT (SELECT COUNT(*) FROM canonical.Customers) customers,(SELECT COUNT(*) FROM canonical.Products) products,
    (SELECT COUNT(*) FROM canonical.SalesInvoices) invoices,(SELECT COUNT(*) FROM warehouse.FactSalesInvoiceItem) facts,
    (SELECT SUM(NetAmount) FROM warehouse.FactSalesInvoiceItem) netRevenue FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`);
  const before = counts();
  assert.deepEqual(before, {
    customers: 6,
    products: 5,
    invoices: 4,
    facts: 6,
    netRevenue: 11110,
  });
  assert(!(verifyVolume && verifyRetry), "Separate volume and retry scenarios");
  let transientSource, volumeSource, child, log;
  const retry = {
    enabled: verifyRetry,
    failedStatusObserved: false,
    queuedRetryObserved: false,
  };
  try {
    if (verifyVolume) volumeSource = await createVolumeDemoSource();
    const expectedAfter = volumeSource
      ? {
          customers: before.customers + volumeSource.sizes.customers,
          products: before.products + volumeSource.sizes.products,
          invoices: before.invoices + volumeSource.sizes["sales-invoices"],
          facts: before.facts + volumeSource.sizes["sales-invoices"],
          netRevenue: before.netRevenue + volumeSource.netRevenue,
        }
      : before;
    if (verifyRetry) transientSource = await createTransientDemoSource(base);
    const endpointFor = (entity) =>
      volumeSource
        ? volumeSource.endpoint(entity)
        : entity === "customers" && transientSource
          ? transientSource.endpoint
          : `${base}/api/v1/sample/${entity}`;
    const workerEnv = { ...env },
      watermarks = {};
    for (const [index, entity] of entities.entries())
      workerEnv[`ReferenceRest__AllowedEndpoints__${index}`] =
        endpointFor(entity);
    for (const connector of selected) {
      watermarks[connector.id] = sql(
        `SELECT LastWatermark watermark FROM connector.ConnectorInstances WHERE Id='${connector.id}' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`,
      ).watermark;
      assert(
        watermarks[connector.id],
        "Restored incremental watermark missing",
      );
      const endpoint = endpointFor(connector.sourceEntity);
      assert(
        /^http:\/\/127\.0\.0\.1:\d+\/(api\/v1\/sample\/)?(customers|products|sales-invoices)$/.test(
          endpoint,
        ),
      );
      const updated = sql(
        `UPDATE connector.ConnectorInstances SET BaseUrl=N'${endpoint}' WHERE TenantId='${tenant}' AND Id='${connector.id}'; SELECT @@ROWCOUNT affected FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`,
      );
      assert.equal(
        updated.affected,
        1,
        "Disposable connector configuration not updated",
      );
    }
    const queues = () =>
      JSON.parse(
        invoke([
          "exec",
          rabbit,
          "rabbitmqctl",
          "-q",
          "--timeout",
          "10",
          "list_queues",
          "name",
          "messages_ready",
          "messages_unacknowledged",
          "consumers",
          "--formatter=json",
        ]),
      );
    log = openSync(join(output, "restored-worker.log"), "wx");
    child = spawn("dotnet", [workerDll], {
      cwd: join(root, "src/Worker"),
      env: workerEnv,
      windowsHide: true,
      stdio: ["ignore", log, log],
    });
    let spawnError;
    child.on("error", (error) => {
      spawnError = error;
    });
    let consumerReady = false;
    for (let i = 0; i < 40; i++) {
      assert(
        !spawnError && child.exitCode === null,
        "Isolated Worker failed to start",
      );
      if (
        queues().find((q) => q.name === "connector.sync.requested")
          ?.consumers === 1
      ) {
        consumerReady = true;
        break;
      }
      await delay(500);
    }
    assert(consumerReady, "Isolated Worker consumer missing");
    const runs = [];
    for (const connector of selected) {
      const requestedAt = Date.now();
      const accepted = await api(
        `/api/v1/connectors/${connector.id}/sync`,
        {},
        token,
      );
      assert(/^[a-f0-9-]{36}$/i.test(accepted.syncRunId));
      let run;
      for (let i = 0; i < (verifyVolume ? 1200 : 120); i++) {
        assert(child.exitCode === null, "Worker exited during synchronization");
        run = await api(
          `/api/v1/connectors/${connector.id}/sync-runs/${accepted.syncRunId}`,
          undefined,
          token,
        );
        const controlledRetry =
          verifyRetry && connector.sourceEntity === "customers";
        if (
          run.status === "Failed" &&
          controlledRetry &&
          !retry.failedStatusObserved
        ) {
          retry.failedStatusObserved = true;
          const failedWatermark = sql(
            `SELECT LastWatermark watermark FROM connector.ConnectorInstances WHERE Id='${connector.id}' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`,
          ).watermark;
          assert.equal(
            failedWatermark,
            watermarks[connector.id],
            "Failed extraction advanced watermark",
          );
          retry.watermarkPreservedDuringFailure = true;
          for (let j = 0; j < 5; j++) {
            const queue = queues().find(
              (q) => q.name === "connector.sync.retry.10s",
            );
            if (queue && queue.messages_ready > 0) {
              retry.queuedRetryObserved = true;
              retry.queueDuringFailure = queue;
              break;
            }
            await delay(100);
          }
        }
        if (
          run.status === "Succeeded" ||
          (run.status === "Failed" && !controlledRetry)
        )
          break;
        await delay(500);
      }
      assert.equal(
        run.status,
        "Succeeded",
        `Restored ${connector.sourceEntity} sync did not succeed`,
      );
      assert.equal(
        run.recordsProcessed,
        volumeSource ? volumeSource.sizes[connector.sourceEntity] : 1,
        "Incremental extraction must receive only the changing Demo record",
      );
      assert.equal(
        run.acceptedCount,
        volumeSource ? volumeSource.sizes[connector.sourceEntity] : 1,
      );
      assert.equal(run.rejectedCount, 0);
      const table = {
        customers: "Customers",
        products: "Products",
        "sales-invoices": "SalesInvoices",
      }[connector.sourceEntity];
      const refs = sql(
        `SELECT DISTINCT RawObjectUri rawKey FROM canonical.${table} WHERE RawObjectUri LIKE '%/${accepted.syncRunId}/%' FOR JSON PATH;`,
      );
      assert.equal(
        refs.length,
        1,
        "Canonical record does not reference new raw",
      );
      const raw = await verifyRaw(refs[0].rawKey);
      if (volumeSource) {
        const requests = volumeSource.requests.filter(
          (r) => r.entity === connector.sourceEntity,
        );
        assert.equal(requests.length, 1, "Expected one bounded source request");
        assert.equal(
          raw.sha256,
          requests[0].sha256,
          "Raw differs from synthetic source",
        );
        assert.equal(raw.bytes, requests[0].bytes);
      }
      assert.equal(
        raw.syncRunId?.toLowerCase(),
        accepted.syncRunId.toLowerCase(),
      );
      const watermark = sql(
        `SELECT LastWatermark watermark FROM connector.ConnectorInstances WHERE Id='${connector.id}' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`,
      ).watermark;
      assert(
        Date.parse(watermark) > Date.parse(watermarks[connector.id]) &&
          Date.parse(watermark) >= requestedAt,
        "Incremental watermark did not advance",
      );
      runs.push({
        elapsedMilliseconds: Date.now() - requestedAt,
        entity: connector.sourceEntity,
        syncRunId: accepted.syncRunId,
        status: run.status,
        received: run.recordsProcessed,
        accepted: run.acceptedCount,
        updated: run.updatedCount,
        rejected: run.rejectedCount,
        watermarkBefore: watermarks[connector.id],
        watermarkAfter: watermark,
        raw,
      });
    }
    const invoiceRaw = runs.find((run) => run.entity === "sales-invoices").raw
      .key;
    assert(!invoiceRaw.includes("'"), "Unexpected quote in generated raw key");
    let updatedFact;
    const warehouseWaitStarted = Date.now();
    for (let i = 0; i < (verifyVolume ? 600 : 40); i++) {
      updatedFact = sql(
        `SELECT MIN(FactSalesInvoiceItemKey) factId FROM warehouse.FactSalesInvoiceItem WHERE RawObjectUri=N'${invoiceRaw}' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`,
      );
      if (updatedFact.factId && counts().facts === expectedAfter.facts) break;
      await delay(500);
    }
    const warehouseWaitMilliseconds = Date.now() - warehouseWaitStarted;
    assert(
      updatedFact.factId,
      "Warehouse does not reference newly ingested raw",
    );
    assert.deepEqual(
      counts(),
      expectedAfter,
      "Incremental upsert duplicated records or changed totals",
    );
    const queryStarted = performance.now();
    const analytics = await api(
      "/api/v1/analytics/query",
      { dataset: "sales", metrics: ["netRevenue"], dimensions: ["date.month"] },
      token,
    );
    const analyticalQueryMilliseconds = +(
      performance.now() - queryStarted
    ).toFixed(3);
    assert.equal(
      analytics.data.reduce((sum, row) => sum + row.netRevenue, 0),
      expectedAfter.netRevenue,
    );
    assert(
      Date.parse(analytics.metadata.dataFreshnessAt) >
        Date.parse(initialFreshness),
      "Analytical freshness did not advance",
    );
    const metrics = await api("/api/v1/data-catalog/metrics", undefined, token);
    assert(
      metrics.every((metric) =>
        metric.reconciliations.some(
          (r) =>
            r.isWithinTolerance &&
            r.canonicalNetAmountTotal === expectedAfter.netRevenue &&
            r.factNetAmountTotal === expectedAfter.netRevenue,
        ),
      ),
      "Reconciliation failed after ingestion",
    );
    const lineage = await api(
      `/api/v1/data-catalog/lineage/${updatedFact.factId}`,
      undefined,
      token,
    );
    assert.equal(lineage.rawObjectUri, invoiceRaw);
    assert.equal(lineage.syncRun?.status, "Succeeded");
    assert.equal(lineage.loadBatch?.status, "Succeeded");
    let finalQueues;
    for (let i = 0; i < 10; i++) {
      finalQueues = queues().filter(
        (q) =>
          q.name.startsWith("connector.sync.requested") ||
          q.name.startsWith("connector.sync.retry."),
      );
      if (
        finalQueues.every(
          (q) => q.messages_ready === 0 && q.messages_unacknowledged === 0,
        )
      )
        break;
      await delay(500);
    }
    assert(
      [
        "connector.sync.requested",
        "connector.sync.requested.dlq",
        "connector.sync.retry.10s",
        "connector.sync.retry.30s",
        "connector.sync.retry.90s",
      ].every((name) => finalQueues.some((q) => q.name === name)) &&
        finalQueues.every(
          (q) => q.messages_ready === 0 && q.messages_unacknowledged === 0,
        ),
      "Isolated queues not drained",
    );
    if (verifyRetry) {
      assert(
        retry.failedStatusObserved && retry.queuedRetryObserved,
        "Failure/retry transition not observed",
      );
      assert.deepEqual(
        transientSource.attempts.map((a) => a.status),
        [503, 200],
      );
      retry.attempts = transientSource.attempts;
      retry.delayMilliseconds =
        Date.parse(retry.attempts[1].at) - Date.parse(retry.attempts[0].at);
      assert(
        retry.delayMilliseconds >= 9000,
        "Expected real 10-second retry backoff",
      );
      retry.sameRunRecovered = true;
    }
    if (verifyVolume) {
      const workerLog = readFileSync(
        join(output, "restored-worker.log"),
        "utf8",
      );
      assert(
        !workerLog.includes("Reconciliação Canônico↔Origem fora da tolerância"),
        "Valid incremental volume emitted an origin/canonical discrepancy",
      );
    }
    return {
      status: "passed",
      workerDllSha256: createHash("sha256")
        .update(readFileSync(workerDll))
        .digest("hex"),
      runs,
      countsBefore: before,
      countsAfter: counts(),
      newRawObjects: runs.length,
      noDuplicateBusinessRows: true,
      freshnessBefore: initialFreshness,
      freshnessAfter: analytics.metadata.dataFreshnessAt,
      lineagePassed: true,
      reconciliationPassed: true,
      queuesDrained: true,
      finalQueues,
      retry,
      volume: volumeSource
        ? {
            sizes: volumeSource.sizes,
            sourceRequests: volumeSource.requests,
            requestPattern:
              "Three sequential full response payloads; one sync per entity; no pacing or concurrency",
            warehouseWaitMilliseconds,
            analyticalQueryMilliseconds,
            originCanonicalWarningAbsent: true,
          }
        : undefined,
      scope: verifyVolume
        ? "Synthetic 200 customers, 50 products and 2000 invoices through isolated raw/canonical/Warehouse/API; additive to restored Demo"
        : "Three incremental Demo syncs via isolated API/Worker/broker to restored SQL/storage; no original endpoints used",
    };
  } finally {
    if (child && child.pid && child.exitCode === null) {
      const exited = once(child, "exit");
      child.kill();
      await exited;
    }
    if (log !== undefined) closeSync(log);
    if (transientSource) await transientSource.close();
    if (volumeSource) await volumeSource.close();
  }
}
