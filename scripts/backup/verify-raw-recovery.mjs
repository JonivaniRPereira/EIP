import assert from "node:assert/strict";
import { createHash, createHmac, randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { verifyRestoredApplication } from "./verify-restored-app.mjs";

const [
  docker,
  sqlContainer,
  sqlRunId,
  tenantInput,
  output,
  hostDll,
  workerDll,
  retryFlag,
] = process.argv.slice(2);
const tenant = tenantInput?.toLowerCase();
assert(/^[a-f0-9-]{36}$/.test(tenant) && /^[a-f0-9]{32}$/.test(sqlRunId));
assert.equal(sqlContainer, `eip-recovery-restored-${sqlRunId}`);
const invoke = (args) => {
  try {
    return execFileSync(docker, args, {
      encoding: "utf8",
      timeout: 120000,
      stdio: ["ignore", "pipe", "pipe"],
    }).trim();
  } catch {
    throw new Error(`Docker operation failed: ${args[0]}`);
  }
};
assert.equal(
  invoke([
    "inspect",
    "--format",
    '{{index .Config.Labels "eip.recovery.run"}}',
    sqlContainer,
  ]),
  sqlRunId,
);
const root = fileURLToPath(new URL("../../", import.meta.url));
const settings = JSON.parse(
  readFileSync(join(root, "src/Host/appsettings.json"), "utf8"),
).DataLake;
const source = new URL(settings.ServiceUrl);
assert(
  ["localhost", "127.0.0.1"].includes(source.hostname) &&
    !source.username &&
    !source.password,
  "Local source storage required",
);
assert(/^[a-z0-9-]{3,63}$/.test(settings.BucketName));
const bucket = settings.BucketName;
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");
const hmac = (key, text) => createHmac("sha256", key).update(text).digest();
const runId = randomUUID().replaceAll("-", "");
const container = `eip-raw-recovery-${runId}`;
const targetCredentials = {
  access: `recovery${runId.slice(0, 12)}`,
  secret: randomUUID() + randomUUID(),
};
const rawDirectory = join(output, "raw");
mkdirSync(rawDirectory);
const sql = (query) =>
  JSON.parse(
    invoke([
      "exec",
      sqlContainer,
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
      `SET NOCOUNT ON; EXEC sys.sp_set_session_context @key=N'TenantId',@value=N'${tenant}'; ${query}`,
    ])
      .replaceAll("\r", "")
      .replaceAll("\n", ""),
  );
// Every persisted RawObjectUri column in canonical/Warehouse for this tenant, including history.
const references = sql(`CREATE TABLE #refs(rawKey nvarchar(2000));
DECLARE @schema sysname,@table sysname,@query nvarchar(max);
DECLARE refs CURSOR LOCAL FAST_FORWARD FOR SELECT s.name,t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN ('canonical','warehouse') AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.name='RawObjectUri')
AND EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.name='TenantId');
OPEN refs; FETCH NEXT FROM refs INTO @schema,@table;
WHILE @@FETCH_STATUS=0 BEGIN SET @query=N'INSERT #refs SELECT RawObjectUri FROM '+QUOTENAME(@schema)+N'.'+QUOTENAME(@table)+N' WHERE TenantId=@tenant';
EXEC sys.sp_executesql @query,N'@tenant uniqueidentifier',@tenant='${tenant}'; FETCH NEXT FROM refs INTO @schema,@table; END;
CLOSE refs; DEALLOCATE refs; SELECT DISTINCT rawKey FROM #refs WHERE rawKey IS NOT NULL ORDER BY rawKey FOR JSON PATH;`);
const keys = references
  .filter((r) => !r.rawKey.startsWith("synthetic://"))
  .map((r) => r.rawKey);
const syntheticReferencesExcluded = references.length - keys.length;
assert(keys.length > 0, "No raw references found for selected organization");
for (const key of keys)
  assert(
    key.toLowerCase().startsWith(tenant + "/") &&
      !key.split("/").includes(".."),
    "Unexpected tenant raw key",
  );
const counts =
  sql(`SELECT (SELECT COUNT(*) FROM canonical.Customers) customers,(SELECT COUNT(*) FROM canonical.Products) products,
(SELECT COUNT(*) FROM canonical.SalesInvoices) invoices,(SELECT COUNT(*) FROM warehouse.FactSalesInvoiceItem) facts,
(SELECT SUM(NetAmount) FROM warehouse.FactSalesInvoiceItem) netRevenue FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`);
assert(
  counts.customers > 0 &&
    counts.products > 0 &&
    counts.invoices > 0 &&
    counts.facts > 0,
  "Expected Demo business records",
);
async function request(
  endpoint,
  credentials,
  method,
  key = "",
  bytes = Buffer.alloc(0),
  metadata = {},
) {
  const date = new Date().toISOString().replace(/[:-]|\.\d{3}/g, ""),
    day = date.slice(0, 8);
  const path =
    "/" +
    bucket +
    (key ? "/" + key.split("/").map(encodeURIComponent).join("/") : "");
  const scope = `${day}/us-east-1/s3/aws4_request`;
  const headers = {
    host: endpoint.host,
    "x-amz-content-sha256": hash(bytes),
    "x-amz-date": date,
    ...metadata,
  };
  const names = Object.keys(headers).sort();
  const canonical = `${method}\n${path}\n\n${names.map((n) => `${n}:${headers[n]}\n`).join("")}\n${names.join(";")}\n${hash(bytes)}`;
  const signing = hmac(
    hmac(hmac(hmac("AWS4" + credentials.secret, day), "us-east-1"), "s3"),
    "aws4_request",
  );
  headers.Authorization = `AWS4-HMAC-SHA256 Credential=${credentials.access}/${scope}, SignedHeaders=${names.join(";")}, Signature=${createHmac(
    "sha256",
    signing,
  )
    .update(`AWS4-HMAC-SHA256\n${date}\n${scope}\n${hash(canonical)}`)
    .digest("hex")}`;
  const response = await fetch(endpoint.origin + path, {
    method,
    headers,
    ...(method === "PUT" ? { body: bytes } : {}),
    redirect: "error",
    signal: AbortSignal.timeout(15000),
  });
  assert(response.ok, `Storage ${method} failed: HTTP ${response.status}`);
  return response;
}
function metadataOf(response) {
  return Object.fromEntries(
    [...response.headers].filter(
      ([name]) => name.startsWith("x-amz-meta-") || name === "content-type",
    ),
  );
}
const manifest = [];
const sourceCredentials = {
  access: settings.AccessKey,
  secret: settings.SecretKey,
};
// Read from source only; each required raw object is backed up before creating the target.
for (const [index, key] of keys.entries()) {
  const response = await request(source, sourceCredentials, "GET", key);
  const bytes = Buffer.from(await response.arrayBuffer());
  const metadata = metadataOf(response);
  assert.equal(
    metadata["x-amz-meta-sha256-checksum"],
    hash(bytes),
    "Source checksum mismatch",
  );
  assert.equal(
    metadata["x-amz-meta-tenant-id"]?.toLowerCase(),
    tenant,
    "Source metadata tenant mismatch",
  );
  JSON.parse(bytes.toString("utf8"));
  const file = `${String(index).padStart(6, "0")}.json`;
  writeFileSync(join(rawDirectory, file), bytes, { flag: "wx" });
  manifest.push({
    key,
    file,
    bytes: bytes.length,
    sha256: hash(bytes),
    metadata,
  });
}
writeFileSync(
  join(rawDirectory, "manifest.json"),
  JSON.stringify(manifest, null, 2),
  { flag: "wx" },
);
let created = false;
let result;
try {
  // No shared volume; only a fresh container filesystem and a loopback ephemeral port.
  invoke([
    "run",
    "-d",
    "--name",
    container,
    "--label",
    `eip.joint.run=${runId}`,
    "-p",
    "127.0.0.1::9000",
    "-e",
    `MINIO_ROOT_USER=${targetCredentials.access}`,
    "-e",
    `MINIO_ROOT_PASSWORD=${targetCredentials.secret}`,
    "eip-minio-source:RELEASE.2025-04-08T15-41-24Z",
    "server",
    "/data",
  ]);
  created = true;
  const target = new URL("http://" + invoke(["port", container, "9000/tcp"]));
  assert.equal(target.hostname, "127.0.0.1");
  let ready = false;
  for (let attempt = 0; attempt < 40; attempt++) {
    try {
      ready = (
        await fetch(target.origin + "/minio/health/ready", {
          signal: AbortSignal.timeout(1000),
        })
      ).ok;
    } catch {}
    if (ready) break;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  assert(ready, "Isolated storage not ready");
  await request(target, targetCredentials, "PUT");
  async function verify(entry) {
    const response = await request(target, targetCredentials, "GET", entry.key);
    const bytes = Buffer.from(await response.arrayBuffer());
    assert.equal(hash(bytes), entry.sha256, "Restored checksum mismatch");
    assert.deepEqual(
      metadataOf(response),
      entry.metadata,
      "Restored metadata mismatch",
    );
    JSON.parse(bytes.toString("utf8"));
  }
  for (const entry of manifest) {
    const bytes = readFileSync(join(rawDirectory, entry.file));
    assert.equal(hash(bytes), entry.sha256);
    await request(
      target,
      targetCredentials,
      "PUT",
      entry.key,
      bytes,
      entry.metadata,
    );
    await verify(entry);
  }
  const first = manifest[0];
  const original = readFileSync(join(rawDirectory, first.file));
  await request(
    target,
    targetCredentials,
    "PUT",
    first.key,
    Buffer.concat([original, Buffer.from(" ")]),
    first.metadata,
  );
  let corruptionDetected = false;
  try {
    await verify(first);
  } catch (error) {
    if (error.message.includes("Restored checksum mismatch"))
      corruptionDetected = true;
    else throw error;
  }
  assert(corruptionDetected, "Corruption was not detected");
  await request(
    target,
    targetCredentials,
    "PUT",
    first.key,
    original,
    first.metadata,
  );
  await verify(first);
  let applicationReadback;
  if (hostDll) {
    const fact = sql(
      "SELECT MIN(FactSalesInvoiceItemKey) factId FROM warehouse.FactSalesInvoiceItem FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;",
    );
    applicationReadback = await verifyRestoredApplication({
      invoke,
      sqlContainer,
      storageContainer: container,
      storageUrl: target,
      credentials: targetCredentials,
      bucket,
      hostDll,
      workerDll,
      verifyRetry: retryFlag === "--verify-retry",
      verifyVolume: retryFlag === "--verify-volume",
      sql,
      verifyRaw: async (key) => {
        assert(
          key.toLowerCase().startsWith(tenant + "/") &&
            !key.split("/").includes(".."),
          "Unexpected new raw key",
        );
        const response = await request(target, targetCredentials, "GET", key);
        const bytes = Buffer.from(await response.arrayBuffer());
        const metadata = metadataOf(response);
        assert.equal(hash(bytes), metadata["x-amz-meta-sha256-checksum"]);
        assert.equal(metadata["x-amz-meta-tenant-id"]?.toLowerCase(), tenant);
        JSON.parse(bytes.toString("utf8"));
        return {
          key,
          bytes: bytes.length,
          sha256: hash(bytes),
          syncRunId: metadata["x-amz-meta-sync-run-id"],
        };
      },
      root,
      output,
      tenant,
      manifest,
      factId: fact.factId,
    });
  }
  result = {
    status: "passed",
    completedAtUtc: new Date().toISOString(),
    scope: "Full SQL backup; raw recovery limited to selected Demo tenant",
    tenantId: tenant,
    rawObjects: manifest.length,
    rawBytes: manifest.reduce((sum, e) => sum + e.bytes, 0),
    syntheticReferencesExcluded,
    sql: JSON.parse(
      readFileSync(join(output, "sql/result.json"), "utf8").replace(
        /^\uFEFF/,
        "",
      ),
    ),
    counts,
    applicationReadback,
    allReferencedRealObjectsRestored: true,
    checksumsMatch: true,
    metadataMatch: true,
    corruptionDetected,
    sourceObjectsModified: false,
    applicationConnectionsChanged: false,
    limitations: [
      ...(hostDll
        ? [
            workerDll
              ? "Development API and Worker only; no Gateway/browser restart"
              : "Development API readback only; no Gateway/browser or Worker restart",
          ]
        : ["No application restart against recovered copies"]),
      "Not an atomic distributed snapshot or recovery of all tenants",
      "No RPO/RTO commitment or broker/cache/secret recovery",
    ],
  };
  console.log(
    `Raw recovery passed: ${manifest.length} objects; corruption detected and repaired in isolated target.`,
  );
} finally {
  if (created) {
    assert.equal(
      invoke([
        "inspect",
        "--format",
        '{{index .Config.Labels "eip.joint.run"}}',
        container,
      ]),
      runId,
    );
    assert.equal(container, `eip-raw-recovery-${runId}`);
    invoke(["rm", "-f", "-v", container]);
  }
}
writeFileSync(
  join(output, "raw-result.json"),
  JSON.stringify(result, null, 2),
  { flag: "wx" },
);
