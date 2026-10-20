import assert from "node:assert/strict";
import { randomUUID, createHash } from "node:crypto";
import { readFileSync, openSync, closeSync } from "node:fs";
import { join } from "node:path";
import { createServer } from "node:net";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { verifyRestoredIngestion } from "./verify-restored-ingestion.mjs";

export async function verifyRestoredApplication({
  invoke,
  sqlContainer,
  storageContainer,
  storageUrl,
  credentials,
  bucket,
  hostDll,
  root,
  output,
  tenant,
  manifest,
  factId,
  workerDll,
  verifyRetry = false,
  verifyVolume = false,
  sql,
  verifyRaw,
}) {
  const runId = randomUUID().replaceAll("-", "");
  const network = `eip-resume-${runId}`;
  const redis = `eip-resume-cache-${runId}`,
    rabbit = `eip-resume-broker-${runId}`;
  const owned = [],
    attached = [];
  let networkCreated = false,
    child,
    log;
  const password = process.env.EIP_RECOVERY_DEMO_PASSWORD;
  assert(
    password && hostDll,
    "Existing Demo password and published Host required",
  );
  const config = JSON.parse(
    readFileSync(join(root, "src/Host/appsettings.json"), "utf8"),
  );
  const portOf = (container, port) => {
    const address = invoke(["port", container, `${port}/tcp`]);
    assert(/^127\.0\.0\.1:\d+$/.test(address), "Loopback mapping required");
    return address.split(":")[1];
  };
  const labelArgs = ["--label", `eip.resume.run=${runId}`];
  const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  try {
    invoke(["network", "create", ...labelArgs, network]);
    networkCreated = true;
    for (const container of [sqlContainer, storageContainer]) {
      invoke(["network", "connect", network, container]);
      attached.push(container);
    }
    invoke([
      "run",
      "-d",
      "--name",
      redis,
      ...labelArgs,
      "--network",
      network,
      "-p",
      "127.0.0.1::6379",
      "redis:7.4-alpine",
    ]);
    owned.push(redis);
    const brokerPassword = randomUUID().replaceAll("-", "");
    invoke([
      "run",
      "-d",
      "--name",
      rabbit,
      ...labelArgs,
      "--network",
      network,
      "-p",
      "127.0.0.1::5672",
      "-e",
      "RABBITMQ_DEFAULT_USER=recovery",
      "-e",
      `RABBITMQ_DEFAULT_PASS=${brokerPassword}`,
      "rabbitmq:3.13-management-alpine",
    ]);
    owned.push(rabbit);
    assert.equal(
      invoke(["exec", redis, "redis-cli", "DBSIZE"]),
      "0",
      "Cache must start empty",
    );
    const sqlPassword = invoke([
      "exec",
      sqlContainer,
      "printenv",
      "MSSQL_SA_PASSWORD",
    ]);
    const connection = `Server=127.0.0.1,${portOf(sqlContainer, 1433)};Database=EIP;User Id=sa;Password=${sqlPassword};TrustServerCertificate=True;`;
    const portServer = createServer();
    await new Promise((resolve, reject) => {
      portServer.once("error", reject);
      portServer.listen(0, "127.0.0.1", resolve);
    });
    const apiPort = portServer.address().port;
    await new Promise((resolve) => portServer.close(resolve));
    const base = `http://127.0.0.1:${apiPort}`;
    const env = {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      DOTNET_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: base,
      Jwt__SigningKey: randomUUID() + randomUUID(),
      ConnectionStrings__Redis: `127.0.0.1:${portOf(redis, 6379)}`,
      ConnectionStrings__RabbitMQ: `amqp://recovery:${brokerPassword}@127.0.0.1:${portOf(rabbit, 5672)}/`,
      DataLake__ServiceUrl: storageUrl.origin,
      DataLake__AccessKey: credentials.access,
      DataLake__SecretKey: credentials.secret,
      DataLake__BucketName: bucket,
    };
    delete env.EIP_RECOVERY_DEMO_PASSWORD;
    for (const name of Object.keys(config.ConnectionStrings).filter((name) =>
      name.endsWith("Db"),
    ))
      env[`ConnectionStrings__${name}`] = connection;
    log = openSync(join(output, "restored-host.log"), "wx");
    child = spawn("dotnet", [hostDll], {
      cwd: join(root, "src/Host"),
      env,
      windowsHide: true,
      stdio: ["ignore", log, log],
    });
    let spawnError;
    child.on("error", (error) => {
      spawnError = error;
    });
    let ready = false;
    for (let i = 0; i < 90; i++) {
      assert(
        !spawnError && child.exitCode === null,
        "Restored Host failed to start; inspect restricted local log",
      );
      try {
        const response = await fetch(base + "/health/ready", {
          signal: AbortSignal.timeout(1500),
        });
        ready =
          response.status === 200 &&
          (await response.json()).status === "Healthy";
      } catch {}
      if (ready) break;
      await delay(1000);
    }
    assert(ready, "Restored application readiness failed");
    async function api(path, body, token) {
      const response = await fetch(base + path, {
        method: body === undefined ? "GET" : "POST",
        redirect: "error",
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
        signal: AbortSignal.timeout(20000),
      });
      assert(
        response.ok,
        `Recovered API request failed: HTTP ${response.status}`,
      );
      return response.json();
    }
    const anonymous = await fetch(base + "/api/v1/dashboards");
    assert.equal(anonymous.status, 401);
    const login = await api("/api/v1/auth/login", {
      email: "demo@eip.local",
      password,
    });
    const token = login.accessToken;
    assert(token, "Login token missing");
    const claims = JSON.parse(
      Buffer.from(token.split(".")[1], "base64url").toString("utf8"),
    );
    assert.equal(
      claims.tenant_id?.toLowerCase(),
      tenant,
      "Recovered login selected another tenant",
    );
    const analytics = await api(
      "/api/v1/analytics/query",
      { dataset: "sales", metrics: ["netRevenue"], dimensions: ["date.month"] },
      token,
    );
    assert.equal(
      analytics.data.reduce((sum, row) => sum + row.netRevenue, 0),
      11110,
    );
    assert(
      analytics.metadata.dataFreshnessAt,
      "Recovered Warehouse freshness missing",
    );
    const cacheKeys = Number(invoke(["exec", redis, "redis-cli", "DBSIZE"]));
    assert(cacheKeys > 0, "Isolated cache was not populated");
    const dashboards = await api("/api/v1/dashboards", undefined, token);
    const dashboard = dashboards.find(
      (item) =>
        item.id.toLowerCase() === "278f810f-b46a-4fac-ab65-aff0927eb803",
    );
    assert(dashboard, "Restored Demo dashboard missing");
    const widgets = await api(
      `/api/v1/dashboards/${dashboard.id}/query`,
      {},
      token,
    );
    assert.equal(
      widgets[0].data.reduce((sum, row) => sum + row.netRevenue, 0),
      11110,
    );
    const lineage = await api(
      `/api/v1/data-catalog/lineage/${factId}`,
      undefined,
      token,
    );
    assert(
      manifest.some((entry) => entry.key === lineage.rawObjectUri),
      "Lineage references an unrestored object",
    );
    const metrics = await api("/api/v1/data-catalog/metrics", undefined, token);
    assert(
      metrics.length > 0 &&
        metrics.every((metric) =>
          metric.reconciliations.some(
            (r) =>
              r.isWithinTolerance &&
              r.canonicalNetAmountTotal === 11110 &&
              r.factNetAmountTotal === 11110,
          ),
        ),
      "Recovered reconciliation mismatch",
    );
    const ingestion = workerDll
      ? await verifyRestoredIngestion({
          api,
          token,
          tenant,
          base,
          env,
          workerDll,
          verifyRetry,
          verifyVolume,
          root,
          output,
          sql,
          verifyRaw,
          invoke,
          rabbit,
          initialFreshness: analytics.metadata.dataFreshnessAt,
        })
      : undefined;
    return {
      status: "passed",
      hostDllSha256: createHash("sha256")
        .update(readFileSync(hostDll))
        .digest("hex"),
      loginPassed: true,
      anonymousDenied: true,
      readinessPassed: true,
      netRevenue: 11110,
      dashboardId: dashboard.id,
      dashboardNetRevenue: 11110,
      lineagePassed: true,
      reconciliationPassed: true,
      cacheInitiallyEmpty: true,
      cacheKeysAfterQuery: cacheKeys,
      isolatedSqlStorageCacheBroker: true,
      ingestion,
      scope: workerDll
        ? "Development Host and Worker on restored copies; no Gateway/browser restart"
        : "Native Development Host API on restored copies; no Gateway, browser or ingestion Worker restart",
    };
  } finally {
    if (child && child.exitCode === null && child.pid) {
      const exited = once(child, "exit");
      child.kill();
      await exited;
    }
    if (log !== undefined) closeSync(log);
    for (const container of owned.reverse()) {
      assert.equal(
        invoke([
          "inspect",
          "--format",
          '{{index .Config.Labels "eip.resume.run"}}',
          container,
        ]),
        runId,
      );
      invoke(["rm", "-f", "-v", container]);
    }
    for (const container of attached.reverse())
      invoke(["network", "disconnect", network, container]);
    if (networkCreated) {
      assert.equal(
        invoke([
          "network",
          "inspect",
          "--format",
          '{{index .Labels "eip.resume.run"}}',
          network,
        ]),
        runId,
      );
      invoke(["network", "rm", network]);
    }
  }
}
