import { Service, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Connector {
  id: string;
  companyId: string;
  name: string;
  baseUrl: string;
  sourceEntity: string;
  status: string;
}
export interface SyncRun {
  id: string;
  connectorInstanceId: string;
  status: string;
  correlationId: string;
  recordsProcessed: number | null;
  acceptedCount: number | null;
  updatedCount: number | null;
  rejectedCount: number | null;
  deletedCount: number | null;
  errorMessage: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
}
export interface Company {
  id: string;
  name: string;
}
export interface HistoryCursor {
  before: string;
  beforeId: string;
}
export interface QuarantineQuery {
  connectorInstanceId?: string;
  includeRequested: boolean;
  cursor?: HistoryCursor;
}

export interface QuarantineEntry {
  id: string;
  connectorInstanceId: string;
  syncRunId: string;
  sourceEntity: string;
  rawObjectUri: string;
  correlationId: string;
  failedRule: string;
  reason: string;
  createdAt: string;
  resolvedAt: string | null;
}
export interface DlqEntry {
  syncRunId: string;
  connectorInstanceId: string;
  correlationId: string;
  contractVersion: string;
  failureReason: string | null;
  deadLetteredAtUtc: string | null;
}

@Service()
export class ConnectorsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/connectors`;
  list(): Promise<Connector[]> {
    return firstValueFrom(this.http.get<Connector[]>(this.url));
  }
  companies(tenantId: string): Promise<Company[]> {
    return firstValueFrom(
      this.http.get<Company[]>(`${environment.apiBaseUrl}/tenants/${tenantId}/companies`),
    );
  }
  register(request: Omit<Connector, 'id' | 'status'>): Promise<Connector> {
    return firstValueFrom(this.http.post<Connector>(this.url, request));
  }
  runs(id: string, cursor?: HistoryCursor): Promise<SyncRun[]> {
    return firstValueFrom(
      this.http.get<SyncRun[]>(`${this.url}/${id}/sync-runs`, { params: { limit: 21, ...cursor } }),
    );
  }
  run(connectorId: string, runId: string): Promise<SyncRun> {
    return firstValueFrom(this.http.get<SyncRun>(`${this.url}/${connectorId}/sync-runs/${runId}`));
  }
  update(id: string, request: { name: string; baseUrl: string }): Promise<Connector> {
    return firstValueFrom(this.http.put<Connector>(`${this.url}/${id}/configuration`, request));
  }
  sync(id: string, reprocessFrom?: string): Promise<{ syncRunId: string }> {
    return firstValueFrom(
      this.http.post<{ syncRunId: string }>(
        `${this.url}/${id}/sync`,
        {},
        reprocessFrom ? { params: { reprocessFrom } } : {},
      ),
    );
  }
  quarantine(query: QuarantineQuery): Promise<QuarantineEntry[]> {
    return firstValueFrom(
      this.http.get<QuarantineEntry[]>(`${this.url}/quarantine`, {
        params: {
          limit: 21,
          includeRequested: query.includeRequested,
          ...(query.connectorInstanceId ? { connectorInstanceId: query.connectorInstanceId } : {}),
          ...query.cursor,
        },
      }),
    );
  }
  dlq(): Promise<DlqEntry[]> {
    return firstValueFrom(this.http.get<DlqEntry[]>(`${this.url}/dlq`));
  }
  reprocess(id: string): Promise<{ syncRunId: string }> {
    return firstValueFrom(
      this.http.post<{ syncRunId: string }>(`${this.url}/quarantine/${id}/reprocess`, {}),
    );
  }
  requeue(id: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.url}/dlq/${id}/requeue`, {}));
  }
}
