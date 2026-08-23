// Espelha EIP.Intelligence.Dashboard.Api.Contracts (src/Intelligence/Dashboard/Api/Contracts/DashboardContracts.cs)
// e EIP.Intelligence.Analytics.Api.Contracts (src/Intelligence/Analytics/Api/Contracts/AnalyticsContracts.cs).
// JSON em camelCase — política padrão do ASP.NET Core para Web API.

export type WidgetType = 'Kpi' | 'LineChart' | 'Table';

export interface FilterClauseRequest {
  field: string;
  operator: string;
  values?: string[];
}

export interface OrderByRequest {
  field: string;
  direction: string;
}

export interface QueryDefinitionRequest {
  dataset?: string;
  metrics?: string[];
  dimensions?: string[];
  filters?: FilterClauseRequest[];
  orderBy?: OrderByRequest[];
  limit?: number;
}

export interface WidgetVisualizationDto {
  unit?: string | null;
  color?: string | null;
}

export interface WidgetRequest {
  type: WidgetType;
  title: string;
  queryDefinition: QueryDefinitionRequest;
  visualization?: WidgetVisualizationDto;
}

export interface CreateDashboardRequest {
  name: string;
}

export interface UpdateDashboardRequest {
  name?: string;
  widgets?: WidgetRequest[];
}

/** Saída — mesmos campos de QueryDefinitionRequest, mas sempre completos (nunca omitidos). */
export interface AnalyticsQueryDefinition {
  dataset: string;
  metrics: string[];
  dimensions: string[];
  filters: FilterClauseRequest[];
  orderBy: OrderByRequest[];
  limit: number | null;
}

export interface WidgetDto {
  type: WidgetType;
  title: string;
  queryDefinition: AnalyticsQueryDefinition;
  visualization: WidgetVisualizationDto;
}

export type DashboardStatus = 'Draft' | 'Published' | 'Archived';

export interface DashboardDto {
  id: string;
  name: string;
  ownerId: string;
  status: DashboardStatus;
  widgets: WidgetDto[];
  createdAt: string;
  updatedAt: string;
}

export interface DashboardSummaryDto {
  id: string;
  name: string;
  status: DashboardStatus;
  updatedAt: string;
}

export interface PublishDashboardResponse {
  dashboardId: string;
  versionId: string;
  publishedAt: string;
}

export interface WidgetQueryMetadataDto {
  dataset: string;
  semanticVersion: string;
  executedAt: string;
  dataFreshnessAt: string | null;
  rowCount: number;
}

export interface WidgetQueryResultDto {
  widgetTitle: string;
  data: Record<string, unknown>[];
  metadata: WidgetQueryMetadataDto;
}

// Catálogo do Analytics Engine (GET /api/v1/analytics/datasets, E1.3).
export interface AnalyticsMetricSummaryDto {
  name: string;
  displayName: string;
  description: string;
}

export interface AnalyticsDimensionSummaryDto {
  name: string;
  displayName: string;
  description: string;
}

export interface AnalyticsDatasetSummaryDto {
  name: string;
  owner: string;
  semanticVersion: string;
  metrics: AnalyticsMetricSummaryDto[];
  dimensions: AnalyticsDimensionSummaryDto[];
}
