// Local-only concurrent Gateway exercise. Requires Node 22+, Docker CLI and the local EIP stack.
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { performance } from 'node:perf_hooks';

const base = process.env.EIP_TEST_GATEWAY || 'http://localhost:5000';
if (!['localhost','127.0.0.1','[::1]'].includes(new URL(base).hostname)) throw Error('Local Gateway required');
const docker = process.env.EIP_DOCKER || join(process.env.LOCALAPPDATA, 'Programs/DockerDesktop/resources/bin/docker.exe');
const runId = randomUUID().replaceAll('-', '');
const output = join(tmpdir(), `eip-isolation-${runId}`);
mkdirSync(output);
const samples = [], failures = [], tenants = [];
function sql(query) {
  return execFileSync(docker, ['exec','eip-sqlserver','sh','-c',
    'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"',
    'sh','-S','localhost','-U','sa','-C','-N','-I','-b','-r','1','-h','-1','-W','-d','EIP','-Q',query],
  {encoding:'utf8',timeout:30000}).trim();
}
async function request(kind, path, body, token, expected) {
  const start = performance.now();
  let status = 0, data, retry;
  try {
    const response = await fetch(base + path, {method:body === undefined ? 'GET':'POST',
      headers:{...(token ? {Authorization:`Bearer ${token}`} : {}),...(body === undefined ? {} : {'Content-Type':'application/json'})},
      body:body === undefined ? undefined : JSON.stringify(body),signal:AbortSignal.timeout(15000)});
    status=response.status; retry=response.headers.get('retry-after');
    const text=await response.text();
    try { data=JSON.parse(text); } catch { data=null; }
  } catch { failures.push({kind,reason:'transport failure'}); }
  samples.push({kind,status,ms:performance.now()-start});
  if (expected && !expected.includes(status)) failures.push({kind,reason:`HTTP ${status}`});
  return {status,data,retry};
}
function check(condition, kind, reason) { if (!condition) failures.push({kind,reason}); }
async function setup(path, body, token) {
  const r=await request('setup',path,body,token,[200,201]);
  if (![200,201].includes(r.status)) throw Error(`Setup failed: ${path} HTTP ${r.status}`);
  return r.data;
}
const query={dataset:'sales',metrics:['netRevenue'],dimensions:['customer']};
try {
  console.log(`Evidence directory: ${output}`);
  for(let i=0;i<4;i++) {
    const email=`isolation-${runId}-${i}@test.local`, password=`Load_${randomUUID()}!`;
    const registered=await setup('/api/v1/auth/register',{email,password,displayName:`Isolation ${i}`});
    const org=await setup('/api/v1/tenants',{name:`Isolation ${runId} ${i}`,slug:`isolation-${runId}-${i}`,countryCode:'BR'},registered.accessToken);
    const auth=await setup('/api/v1/auth/refresh',{refreshToken:registered.refreshToken});
    const connector=await setup('/api/v1/connectors',{companyId:org.companyId,name:'Isolation synthetic',baseUrl:'http://localhost:5080/api/v1/sample/customers',sourceEntity:'customers'},auth.accessToken);
    const tenant={...org,email,password,token:auth.accessToken,connectorId:connector.id,customerId:randomUUID(),label:`Isolation-${i}`,amount:1001+i*1000};
    // Only generated GUIDs and numeric sentinels are interpolated. No production rows are modified.
    sql(`SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
      EXEC sys.sp_set_session_context @key=N'TenantId',@value='${org.tenantId}';
      INSERT warehouse.DimCustomer (TenantId,CustomerId,Code,Name,IsActive,EffectiveFrom,IsCurrent)
      VALUES ('${org.tenantId}','${tenant.customerId}','SYNTHETIC','Isolation-${i}',1,SYSDATETIMEOFFSET(),1);
      DECLARE @customer int=SCOPE_IDENTITY();
      INSERT warehouse.DimProduct (TenantId,ProductId,Code,Name,ProductType,UnitOfMeasure,IsActive,EffectiveFrom,IsCurrent)
      VALUES ('${org.tenantId}',NEWID(),'SYNTHETIC','Isolation product','Product','UN',1,SYSDATETIMEOFFSET(),1);
      DECLARE @product int=SCOPE_IDENTITY();
      INSERT warehouse.FactSalesInvoiceItem (TenantId,TenantKey,CompanyKey,DateKey,CustomerKey,ProductKey,CurrencyKey,SourceSystemId,SourceEntity,SourceRecordId,SalesInvoiceId,SalesInvoiceItemId,RawObjectUri,LoadBatchId,InvoiceNumber,LineNumber,Quantity,GrossAmount,DiscountAmount,TaxAmount,NetAmount,LoadedAt,Status)
      VALUES ('${org.tenantId}',0,0,20261019,@customer,@product,0,NEWID(),'synthetic','${runId}',NEWID(),NEWID(),'synthetic://${runId}',NEWID(),'SYNTHETIC',1,1,${tenant.amount},0,0,${tenant.amount},SYSDATETIMEOFFSET(),'Issued');
      COMMIT;`);
    tenants.push(tenant);
  }
  // RLS itself, with no WHERE TenantId: switch context repeatedly on the same SQL connection.
  let probe='SET NOCOUNT ON;';
  for(let n=0;n<40;n++) {
    const t=tenants[n%4];
    probe+=`EXEC sys.sp_set_session_context @key=N'TenantId',@value='${t.tenantId}';
      IF (SELECT COUNT(*) FROM warehouse.DimCustomer)<>1 OR EXISTS(SELECT 1 FROM warehouse.DimCustomer WHERE CustomerId<>'${t.customerId}') THROW 51000,'RLS isolation failure',1;`;
  }
  probe+="EXEC sys.sp_set_session_context @key=N'TenantId',@value=NULL; IF EXISTS(SELECT 1 FROM warehouse.DimCustomer) THROW 51001,'Missing context leakage',1; SELECT 'RLS_OK';";
  check(sql(probe).includes('RLS_OK'),'rls','unfiltered SQL probe');
  await new Promise(resolve=>setTimeout(resolve,11000));
  for(const t of tenants) {
    const r=await request('initial-sync',`/api/v1/connectors/${t.connectorId}/sync`,{},t.token,[202]);
    t.syncRunId=r.data?.syncRunId;
    if (!t.syncRunId) throw Error('No sync run returned');
  }
  const started=performance.now();
  const startIndex=samples.length;
  for(let round=0;round<5;round++) {
  if(round) await new Promise(resolve=>setTimeout(resolve,11000));
  await Promise.all(Array.from({length:8},async (_,worker)=>{
    const t=tenants[worker%4], other=tenants[(worker+1)%4];
      const auth=await request('login','/api/v1/auth/login',{email:t.email,password:t.password},undefined,[200]);
      check(typeof auth.data?.accessToken==='string','login','missing access token');
      const token=auth.data?.accessToken || t.token;
      const results=await Promise.all([
        request('analytics','/api/v1/analytics/query',query,token,[200]),
        request('foreign-run',`/api/v1/connectors/${other.connectorId}/sync-runs/${other.syncRunId}`,undefined,token,[404]),
        request('foreign-sync',`/api/v1/connectors/${other.connectorId}/sync`,{},token,[400]),
        request('quota',`/api/v1/connectors/${t.connectorId}/sync`,{},token,[429]),
        request('own-run',`/api/v1/connectors/${t.connectorId}/sync-runs/${t.syncRunId}`,undefined,token,[200]),
        request('invalid-query','/api/v1/analytics/query',{...query,metrics:['unknownMetric']},token,[400]),
      ]);
      const rows=results[0].data?.data;
      check(Array.isArray(rows) && rows.length===1 && rows[0].customer===t.label && rows[0].netRevenue===t.amount,
        'analytics','sentinel mismatch or foreign data');
      check(Number(results[3].retry)>0 && Number(results[3].retry)<=60,'quota','missing/invalid Retry-After');
      check(results[4].data?.id===t.syncRunId || results[4].data?.syncRunId===t.syncRunId,'own-run','wrong run returned');
  }));
  }
  const elapsed=(performance.now()-started)/1000;
  const load=samples.slice(startIndex);
  const groups={};
  for(const s of load) (groups[s.kind] ||= []).push(s);
  const statistics=Object.fromEntries(Object.entries(groups).map(([kind,values])=>{
    const times=values.map(s=>s.ms).sort((a,b)=>a-b);
    return [kind,{requests:values.length,statuses:values.reduce((a,s)=>(a[s.status]=(a[s.status]||0)+1,a),{}),
      p95Ms:+times[Math.ceil(times.length*.95)-1].toFixed(3),p99Ms:+times[Math.ceil(times.length*.99)-1].toFixed(3)}];
  }));
  // Separate overload scenario: no identity or payload is needed to verify the IP limiter.
  const burst=await Promise.all(Array.from({length:120},()=>request('gateway-burst','/api/v1/analytics/datasets',undefined,undefined,[401,429])));
  const rejected=burst.filter(r=>r.status===429).length;
  check(rejected>0,'gateway-burst','IP limiter did not reject excess requests');
  const report={status:failures.length?'failed':'passed',completedAtUtc:new Date().toISOString(),runId,
    tenants:tenants.map(t=>({tenantId:t.tenantId,connectorId:t.connectorId,customerId:t.customerId,syncRunId:t.syncRunId,expectedNetRevenue:t.amount})),
    workers:8,iterationsPerWorker:5,roundDelayMs:11000,requests:load.length,elapsedSeconds:+elapsed.toFixed(3),requestsPerSecond:+(load.length/elapsed).toFixed(3),statistics,
    gatewayBurst:{requests:120,rejected,unauthorized:burst.filter(r=>r.status===401).length},
    failures,rlsContextSwitches:40,rawStorageExercised:false,workerCompletionAsserted:false,
    retainedSyntheticData:true,scope:'Gateway auth, populated analytics/cache, sync acceptance and quota, cross-tenant access; direct SQL unfiltered RLS context switching'};
  writeFileSync(join(output,'result.json'),JSON.stringify(report,null,2)+'\n');
  writeFileSync(join(output,'samples.json'),JSON.stringify(load,null,2)+'\n');
  console.log(JSON.stringify({...report,failures:failures.slice(0,12)},null,2));
  if(failures.length) process.exitCode=1;
} catch(error) {
  // Never log response bodies, tokens, passwords or SQL connection secrets.
  writeFileSync(join(output,'failure.json'),JSON.stringify({runId,error:error.message,failures},null,2));
  console.error(error.message); process.exitCode=1;
}
