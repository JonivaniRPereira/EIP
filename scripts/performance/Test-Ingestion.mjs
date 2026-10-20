// Real local Gateway + Worker + SQL + MinIO smoke, using a new synthetic organization.
import {randomUUID,createHash,createHmac} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
const base='http://localhost:5000', id=randomUUID().replaceAll('-','');
const output=join(tmpdir(),`eip-ingestion-${id}`); mkdirSync(output);
const docker=join(process.env.LOCALAPPDATA,'Programs/DockerDesktop/resources/bin/docker.exe');
const result={runId:id,startedAtUtc:new Date().toISOString(),syncs:[]};
let token;
const wait=ms=>new Promise(r=>setTimeout(r,ms));
const assert=(ok,message)=>{if(!ok)throw Error(message);};
async function api(path,body,method=body===undefined?'GET':'POST') {
  const response=await fetch(base+path,{method,headers:{...(token?{Authorization:`Bearer ${token}`} : {}),'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(15000)});
  const text=await response.text();
  assert(response.ok,`HTTP ${response.status} at ${path}`);
  return text?JSON.parse(text):null;
}
function sql(query) {
  const text=execFileSync(docker,['exec','eip-sqlserver','sh','-c','SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd "$@"',
    'sh','-S','localhost','-U','sa','-C','-N','-I','-b','-r','1','-y','0','-w','65535','-d','EIP','-Q',
    `SET NOCOUNT ON; EXEC sys.sp_set_session_context @key=N'TenantId',@value='${result.tenantId}'; ${query}`],{encoding:'utf8',timeout:30000});
  return JSON.parse(text.replaceAll('\r','').replaceAll('\n','').trim());
}
async function sync(connector) {
  const accepted=await api(`/api/v1/connectors/${connector.id}/sync`,{});
  const deadline=Date.now()+180000;
  while(Date.now()<deadline) {
    const run=await api(`/api/v1/connectors/${connector.id}/sync-runs/${accepted.syncRunId}`);
    if(run.status==='Succeeded') {result.syncs.push({entity:connector.sourceEntity,...run});return run;}
    if(run.status==='Failed') throw Error(`Sync failed for ${connector.sourceEntity}`);
    await wait(1500);
  }
  throw Error(`Sync timed out for ${connector.sourceEntity}`);
}
const hash=value=>createHash('sha256').update(value).digest('hex');
const hmac=(key,value)=>createHmac('sha256',key).update(value).digest();
async function rawObject(key) {
  const settings=JSON.parse(readFileSync('src/Host/appsettings.json','utf8')).DataLake;
  const origin=new URL(settings.ServiceUrl); assert(origin.hostname==='localhost','Local storage required');
  const path='/'+settings.BucketName+'/'+key.split('/').map(encodeURIComponent).join('/');
  const date=new Date().toISOString().replace(/[:-]|\.\d{3}/g,''), day=date.slice(0,8);
  const payload=hash(''), scope=`${day}/us-east-1/s3/aws4_request`;
  const headers=`host:${origin.host}\nx-amz-content-sha256:${payload}\nx-amz-date:${date}\n`;
  const signed='host;x-amz-content-sha256;x-amz-date';
  const canonical=`GET\n${path}\n\n${headers}\n${signed}\n${payload}`;
  const signing=hmac(hmac(hmac(hmac('AWS4'+settings.SecretKey,day),'us-east-1'),'s3'),'aws4_request');
  const signature=createHmac('sha256',signing).update(`AWS4-HMAC-SHA256\n${date}\n${scope}\n${hash(canonical)}`).digest('hex');
  const response=await fetch(origin.origin+path,{headers:{'x-amz-date':date,'x-amz-content-sha256':payload,
    Authorization:`AWS4-HMAC-SHA256 Credential=${settings.AccessKey}/${scope}, SignedHeaders=${signed}, Signature=${signature}`},signal:AbortSignal.timeout(15000)});
  assert(response.status===200,`Raw object GET returned ${response.status}`);
  const bytes=Buffer.from(await response.arrayBuffer());
  JSON.parse(bytes.toString('utf8'));
  assert(response.headers.get('x-amz-meta-sha256-checksum')===hash(bytes),'Raw checksum mismatch');
  return {key,bytes:bytes.length,sha256:hash(bytes)};
}
try {
  console.log(`Evidence directory: ${output}`);
  const auth=await api('/api/v1/auth/register',{email:`ingestion-${id}@test.local`,password:`Ingest_${randomUUID()}!`,displayName:'Ingestion synthetic'});
  token=auth.accessToken;
  const org=await api('/api/v1/tenants',{name:`Ingestion ${id}`,slug:`ingestion-${id}`,countryCode:'BR'});
  result.tenantId=org.tenantId; result.companyId=org.companyId;
  token=(await api('/api/v1/auth/refresh',{refreshToken:auth.refreshToken})).accessToken;
  for(const entity of ['customers','products','sales-invoices']) {
    const connector=await api('/api/v1/connectors',{companyId:org.companyId,name:`Ingestion ${entity}`,sourceEntity:entity,baseUrl:`http://localhost:5080/api/v1/sample/${entity}`});
    await sync(connector);
  }
  const deadline=Date.now()+30000;
  let counts;
  do {
    counts=sql(`SELECT (SELECT COUNT(*) FROM canonical.Customers) customers,(SELECT COUNT(*) FROM canonical.Products) products,
      (SELECT COUNT(*) FROM canonical.SalesInvoices) invoices,(SELECT COUNT(*) FROM canonical.SalesInvoiceItems) items,
      (SELECT COUNT(*) FROM warehouse.FactSalesInvoiceItem) facts,(SELECT SUM(NetAmount) FROM warehouse.FactSalesInvoiceItem) netRevenue,
      (SELECT COUNT(*) FROM canonical.QuarantineEntries) quarantined FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`);
    if(counts.facts===6)break; await wait(1000);
  }while(Date.now()<deadline);
  assert(counts.customers===6 && counts.products===5 && counts.invoices===4 && counts.items===6 && counts.facts===6 && counts.netRevenue===11110 && counts.quarantined===0,'Unexpected persisted counts/totals');
  result.counts=counts;
  const analytics=await api('/api/v1/analytics/query',{dataset:'sales',metrics:['netRevenue'],dimensions:['date.month']});
  assert(analytics.data.length===1 && analytics.data[0].netRevenue===11110,'Analytics mismatch');
  assert(analytics.metadata.dataFreshnessAt,'No Warehouse freshness');
  result.analytics=analytics;
  const facts=sql('SELECT FactSalesInvoiceItemKey,RawObjectUri FROM warehouse.FactSalesInvoiceItem ORDER BY FactSalesInvoiceItemKey FOR JSON PATH;');
  result.lineage=await api(`/api/v1/data-catalog/lineage/${facts[0].FactSalesInvoiceItemKey}`);
  assert(JSON.stringify(result.lineage).includes(facts[0].RawObjectUri),'Lineage raw key mismatch');
  assert(result.lineage.syncRun?.status==='Succeeded' && result.lineage.loadBatch?.status==='Succeeded' && result.lineage.canonicalRecord?.rawObjectUri===facts[0].RawObjectUri,'Incomplete lineage');
  result.metrics=await api('/api/v1/data-catalog/metrics');
  assert(result.metrics.length===3 && result.metrics.every(m=>m.reconciliations.length===1 && m.reconciliations[0].isWithinTolerance && m.reconciliations[0].canonicalNetAmountTotal===11110 && m.reconciliations[0].factNetAmountTotal===11110),'Reconciliation mismatch');
  const keys=sql('SELECT RawObjectUri FROM (SELECT RawObjectUri FROM canonical.Customers UNION SELECT RawObjectUri FROM canonical.Products UNION SELECT RawObjectUri FROM canonical.SalesInvoices) k FOR JSON PATH;');
  assert(keys.length===3,'Expected three distinct raw objects');
  result.rawObjects=[]; for(const row of keys)result.rawObjects.push(await rawObject(row.RawObjectUri));
  const dashboard=await api('/api/v1/dashboards',{name:'Ingestion validation'});
  await api(`/api/v1/dashboards/${dashboard.id}`,{widgets:[{type:'Kpi',title:'Receita validada',queryDefinition:{dataset:'sales',metrics:['netRevenue'],dimensions:['date.month']},visualization:{unit:'BRL'}}]},'PATCH');
  const widgetResults=await api(`/api/v1/dashboards/${dashboard.id}/query`,{});
  assert(widgetResults.length===1 && widgetResults[0].data[0].netRevenue===11110,'Dashboard mismatch');
  const published=await api(`/api/v1/dashboards/${dashboard.id}/publish`,{});
  assert(published.versionId,'Dashboard not published');
  result.dashboard={id:dashboard.id,versionId:published.versionId,netRevenue:widgetResults[0].data[0].netRevenue};
  result.status='passed'; result.completedAtUtc=new Date().toISOString();
  writeFileSync(join(output,'result.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({status:result.status,counts,rawObjects:result.rawObjects.length,tenantId:result.tenantId}));
} catch(error) {
  result.status='failed';result.error=error.message;
  writeFileSync(join(output,'result.json'),JSON.stringify(result,null,2)+'\n');
  console.error(error.message);process.exitCode=1;
}
