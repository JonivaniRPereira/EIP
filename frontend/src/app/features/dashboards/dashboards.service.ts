import { Service, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AnalyticsDatasetSummaryDto,
  CreateDashboardRequest,
  DashboardDto,
  DashboardSummaryDto,
  PublishDashboardResponse,
  UpdateDashboardRequest,
  WidgetQueryResultDto,
} from './models';

/** Centraliza as chamadas HTTP do Dashboard Builder (E4) — mesmo papel de AuthService, só que sem
 * estado próprio (nenhuma tela precisa do resultado de uma chamada depois de navegar para outra). */
@Service()
export class DashboardsService {
  private readonly http = inject(HttpClient);

  list(): Promise<DashboardSummaryDto[]> {
    return firstValueFrom(this.http.get<DashboardSummaryDto[]>(`${environment.apiBaseUrl}/dashboards`));
  }

  getById(id: string): Promise<DashboardDto> {
    return firstValueFrom(this.http.get<DashboardDto>(`${environment.apiBaseUrl}/dashboards/${id}`));
  }

  create(request: CreateDashboardRequest): Promise<DashboardDto> {
    return firstValueFrom(this.http.post<DashboardDto>(`${environment.apiBaseUrl}/dashboards`, request));
  }

  update(id: string, request: UpdateDashboardRequest): Promise<DashboardDto> {
    return firstValueFrom(this.http.patch<DashboardDto>(`${environment.apiBaseUrl}/dashboards/${id}`, request));
  }

  publish(id: string): Promise<PublishDashboardResponse> {
    return firstValueFrom(this.http.post<PublishDashboardResponse>(`${environment.apiBaseUrl}/dashboards/${id}/publish`, {}));
  }

  archive(id: string): Promise<DashboardDto> {
    return firstValueFrom(this.http.post<DashboardDto>(`${environment.apiBaseUrl}/dashboards/${id}/archive`, {}));
  }

  query(id: string): Promise<WidgetQueryResultDto[]> {
    return firstValueFrom(this.http.post<WidgetQueryResultDto[]>(`${environment.apiBaseUrl}/dashboards/${id}/query`, {}));
  }

  listDatasets(): Promise<AnalyticsDatasetSummaryDto[]> {
    return firstValueFrom(this.http.get<AnalyticsDatasetSummaryDto[]>(`${environment.apiBaseUrl}/analytics/datasets`));
  }
}
