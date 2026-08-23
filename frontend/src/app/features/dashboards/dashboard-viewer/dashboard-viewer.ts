import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { EchartLine } from '../../../shared/echart-line/echart-line';
import { DashboardsService } from '../dashboards.service';
import { DashboardDto, WidgetDto, WidgetQueryResultDto } from '../models';

interface WidgetView {
  widget: WidgetDto;
  result: WidgetQueryResultDto;
  dimensionKey: string;
  metricKey: string;
  tableColumns: string[];
}

@Component({
  selector: 'app-dashboard-viewer',
  imports: [RouterLink, DatePipe, DecimalPipe, MatButtonModule, MatProgressSpinnerModule, EchartLine],
  templateUrl: './dashboard-viewer.html',
  styleUrl: './dashboard-viewer.scss',
})
export class DashboardViewer implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly dashboardsService = inject(DashboardsService);

  readonly dashboard = signal<DashboardDto | null>(null);
  readonly widgetViews = signal<WidgetView[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  /** Frescor dos dados (docs/12 §10) — o mesmo em todos os widgets nesta fase (1 dataset só, sales),
   * então basta olhar o primeiro resultado. */
  readonly dataFreshnessAt = computed(() => this.widgetViews()[0]?.result.metadata.dataFreshnessAt ?? null);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage.set('Painel não encontrado.');
      this.loading.set(false);
      return;
    }

    try {
      const [dashboard, results] = await Promise.all([
        this.dashboardsService.getById(id),
        this.dashboardsService.query(id),
      ]);

      this.dashboard.set(dashboard);
      this.widgetViews.set(dashboard.widgets.map((widget, index) => this.toWidgetView(widget, results[index])));
    } catch {
      this.errorMessage.set('Não foi possível carregar este painel.');
    } finally {
      this.loading.set(false);
    }
  }

  /** Soma o valor da métrica principal em todas as linhas — um KPI é sempre 1 número, mas o Analytics
   * Engine (E1.2) sempre agrupa por exatamente 1 dimensão, então uma consulta com mais de 1 grupo vira
   * um total agregado (comportamento honesto: nunca mostra só a primeira linha silenciosamente). */
  kpiValue(view: WidgetView): number {
    return view.result.data.reduce((total, row) => total + (Number(row[view.metricKey]) || 0), 0);
  }

  kpiCaption(view: WidgetView): string {
    const groups = view.result.data.length;
    return groups > 1 ? `Soma de ${groups} grupos (${view.dimensionKey})` : view.widget.title;
  }

  lineCategories(view: WidgetView): string[] {
    return view.result.data.map((row) => String(row[view.dimensionKey] ?? ''));
  }

  lineValues(view: WidgetView): number[] {
    return view.result.data.map((row) => Number(row[view.metricKey]) || 0);
  }

  filterSummary(widget: WidgetDto): string | null {
    if (widget.queryDefinition.filters.length === 0) {
      return null;
    }

    return widget.queryDefinition.filters
      .map((filter) => `${filter.field} ${filter.operator} ${(filter.values ?? []).join(', ')}`)
      .join(' · ');
  }

  private toWidgetView(widget: WidgetDto, result: WidgetQueryResultDto): WidgetView {
    const dimensionKey = widget.queryDefinition.dimensions[0] ?? '';
    const metricKey = widget.queryDefinition.metrics[0] ?? '';
    const tableColumns = [dimensionKey, ...widget.queryDefinition.metrics].filter((column) => column.length > 0);

    return { widget, result, dimensionKey, metricKey, tableColumns };
  }
}
