// Local Warehouse/query exercise. Inserts only into newly provisioned synthetic organizations.
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { performance } from "node:perf_hooks";

const base = "http://127.0.0.1:5000";
const docker = join(
  process.env.LOCALAPPDATA,
  "Programs/DockerDesktop/resources/bin/docker.exe",
);
const runId = randomUUID().replaceAll("-", "");
const output = join(tmpdir(), `eip-volume-${runId}`);
mkdirSync(output);
const rowsPerTenant = 10000,
  customersPerTenant = 20,
  workers = 4,
  iterations = 30;
const tenants = [],
  samples = [],
  failures = [];
let attemptedRequests = 0;
const result = {
  runId,
  startedAtUtc: new Date().toISOString(),
  rowsPerTenant,
  workers,
  iterationsPerWorker: iterations,
  scope:
    "Direct synthetic Warehouse seed and real Gateway analytics; ingestion/canonical/raw not exercised",
  retainedSyntheticData: true,
};
console.log(`Evidence directory: ${output}`);
function sql(tenant, query) {
  assert(/^[a-f0-9-]{36}$/i.test(tenant));
  try {
    const text = execFileSync(
      docker,
      [
        "exec",
        "eip-sqlserver",
        "sh",
        "-c",
        'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"',
        "sh",
        "-S",
        "localhost",
        "-U",
        "sa",
        "-C",
        "-N",
        "-I",
        "-b",
        "-r",
        "1",
        "-y",
        "0",
        "-w",
        "65535",
        "-d",
        "EIP",
        "-Q",
        `SET NOCOUNT ON; SET XACT_ABORT ON; EXEC sys.sp_set_session_context @key=N'TenantId',@value=N'${tenant}'; ${query}`,
      ],
      { encoding: "utf8", timeout: 60000, stdio: ["ignore", "pipe", "pipe"] },
    );
    return text.trim()
      ? JSON.parse(text.replaceAll("\r", "").replaceAll("\n", ""))
      : null;
  } catch {
    throw new Error(
      "SQL volume operation failed; synthetic organizations retained",
    );
  }
}
async function api(path, body, token) {
  const start = performance.now();
  const response = await fetch(base + path, {
    method: "POST",
    redirect: "error",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
    signal: AbortSignal.timeout(30000),
  });
  const data = await response.json();
  return { status: response.status, ms: performance.now() - start, data };
}
async function setup(path, body, token) {
  const response = await api(path, body, token);
  assert([200, 201].includes(response.status), `Setup HTTP ${response.status}`);
  return response.data;
}
function verify(tenant, response) {
  assert.equal(response.status, 200, `Query HTTP ${response.status}`);
  assert.equal(
    response.data.data.length,
    customersPerTenant,
    "Unexpected customer count",
  );
  const actual = Object.fromEntries(
    response.data.data.map((row) => [row.customer, row.netRevenue]),
  );
  assert.equal(Object.keys(actual).length, customersPerTenant);
  assert.deepEqual(
    actual,
    tenant.expected,
    "Revenue or tenant isolation mismatch",
  );
  assert.equal(
    response.data.metadata.dataFreshnessAt,
    null,
    "Direct fixture must not claim a successful Warehouse load",
  );
}
try {
  result.tenants = [];
  for (let i = 0; i < 2; i++) {
    const auth = await setup("/api/v1/auth/register", {
      email: `volume-${runId}-${i}@test.local`,
      password: `Volume_${randomUUID()}!`,
      displayName: `Volume synthetic ${i}`,
    });
    const org = await setup(
      "/api/v1/tenants",
      {
        name: `Volume ${runId} ${i}`,
        slug: `volume-${runId}-${i}`,
        countryCode: "BR",
      },
      auth.accessToken,
    );
    const current = await setup("/api/v1/auth/refresh", {
      refreshToken: auth.refreshToken,
    });
    const tenant = {
      tenantId: org.tenantId,
      token: current.accessToken,
      expected: {},
    };
    const baseAmount = (i + 1) * 100;
    for (let n = 1; n <= rowsPerTenant; n++) {
      const label = `Volume-${i}-C${(n - 1) % customersPerTenant}`;
      tenant.expected[label] =
        (tenant.expected[label] ?? 0) + baseAmount + (n % 100);
    }
    const seedStart = performance.now();
    sql(
      org.tenantId,
      `BEGIN TRANSACTION;
      SELECT TOP (${rowsPerTenant}) ROW_NUMBER() OVER(ORDER BY a.object_id,b.object_id) n INTO #numbers FROM sys.all_objects a CROSS JOIN sys.all_objects b;
      CREATE TABLE #customers(CustomerKey int,Code nvarchar(100));
      INSERT warehouse.DimCustomer(TenantId,CustomerId,Code,Name,IsActive,EffectiveFrom,IsCurrent)
      OUTPUT inserted.CustomerKey,inserted.Code INTO #customers
      SELECT '${org.tenantId}',NEWID(),CONCAT('C',n-1),CONCAT('Volume-${i}-C',n-1),1,SYSDATETIMEOFFSET(),1 FROM #numbers WHERE n<=${customersPerTenant};
      INSERT warehouse.DimProduct(TenantId,ProductId,Code,Name,ProductType,UnitOfMeasure,IsActive,EffectiveFrom,IsCurrent)
      VALUES('${org.tenantId}',NEWID(),'SYNTHETIC','Volume synthetic product','Product','UN',1,SYSDATETIMEOFFSET(),1);
      DECLARE @product int=SCOPE_IDENTITY(),@source uniqueidentifier=NEWID(),@batch uniqueidentifier=NEWID();
      INSERT warehouse.FactSalesInvoiceItem(TenantId,TenantKey,CompanyKey,DateKey,CustomerKey,ProductKey,CurrencyKey,SourceSystemId,SourceEntity,SourceRecordId,
      SalesInvoiceId,SalesInvoiceItemId,RawObjectUri,LoadBatchId,InvoiceNumber,LineNumber,Quantity,GrossAmount,DiscountAmount,TaxAmount,NetAmount,LoadedAt,Status)
      SELECT '${org.tenantId}',0,0,20261019,c.CustomerKey,@product,0,@source,'synthetic-volume',CONVERT(nvarchar(20),n),
      NEWID(),NEWID(),'synthetic://${runId}',@batch,CONCAT('VOL-',n),1,1,${baseAmount}+n%100,0,0,${baseAmount}+n%100,SYSDATETIMEOFFSET(),'Issued'
      FROM #numbers JOIN #customers c ON c.Code=CONCAT('C',(n-1)%${customersPerTenant}); COMMIT;`,
    );
    const actual = sql(
      org.tenantId,
      "SELECT COUNT(*) facts,SUM(NetAmount) netRevenue FROM warehouse.FactSalesInvoiceItem FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;",
    );
    const expectedTotal = Object.values(tenant.expected).reduce(
      (sum, amount) => sum + amount,
      0,
    );
    assert.equal(actual.facts, rowsPerTenant);
    assert.equal(actual.netRevenue, expectedTotal);
    tenants.push(tenant);
    result.tenants.push({
      tenantId: tenant.tenantId,
      facts: actual.facts,
      expectedNetRevenue: expectedTotal,
      seedMs: +(performance.now() - seedStart).toFixed(2),
    });
  }
  const query = (limit) => ({
    dataset: "sales",
    metrics: ["netRevenue"],
    dimensions: ["customer"],
    limit,
  });
  result.firstReads = [];
  for (const tenant of tenants) {
    const response = await api(
      "/api/v1/analytics/query",
      query(20),
      tenant.token,
    );
    verify(tenant, response);
    result.firstReads.push({
      tenantId: tenant.tenantId,
      ms: +response.ms.toFixed(2),
    });
  }
  const started = performance.now();
  await Promise.all(
    Array.from({ length: workers }, async (_, worker) => {
      const tenant = tenants[worker % tenants.length];
      for (let iteration = 0; iteration < iterations; iteration++) {
        attemptedRequests++;
        try {
          // Vary query cache keys without changing the expected 20 customer aggregates.
          const response = await api(
            "/api/v1/analytics/query",
            query(20 + iteration),
            tenant.token,
          );
          samples.push({
            worker,
            iteration,
            status: response.status,
            ms: +response.ms.toFixed(3),
          });
          verify(tenant, response);
        } catch (error) {
          failures.push({ worker, iteration, error: error.message });
        }
        await new Promise((resolve) => setTimeout(resolve, 1000));
      }
    }),
  );
  const elapsed = (performance.now() - started) / 1000;
  assert.equal(
    attemptedRequests,
    workers * iterations,
    "Incomplete sample collection",
  );
  const times = samples.map((s) => s.ms).sort((a, b) => a - b);
  result.status = failures.length ? "failed" : "passed";
  result.completedAtUtc = new Date().toISOString();
  result.requests = attemptedRequests;
  result.responsesReceived = samples.length;
  result.elapsedSeconds = +elapsed.toFixed(3);
  result.requestsPerSecond = +(samples.length / elapsed).toFixed(3);
  result.clientLatency = Object.fromEntries(
    [50, 95, 99].map((p) => [
      `p${p}Ms`,
      times[Math.ceil((times.length * p) / 100) - 1],
    ]),
  );
  result.failures = failures;
  result.limits = [
    "Two synthetic tenants, one aggregate dimension and mixed cache; not capacity certification",
    "One-second pacing per user; not maximum throughput",
    "No canonical/ingestion/raw recovery or monthly SLO proof",
  ];
  writeFileSync(join(output, "result.json"), JSON.stringify(result, null, 2));
  writeFileSync(join(output, "samples.json"), JSON.stringify(samples, null, 2));
  console.log(JSON.stringify(result, null, 2));
  if (failures.length) process.exitCode = 1;
} catch (error) {
  result.status = "failed";
  result.error = error.message;
  result.failures = failures;
  writeFileSync(join(output, "result.json"), JSON.stringify(result, null, 2));
  console.error(error.message);
  process.exitCode = 1;
}
