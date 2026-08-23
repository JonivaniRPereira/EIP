import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

import { ProblemDetails } from '../../../core/auth/models';
import { DashboardsService } from '../dashboards.service';
import { AnalyticsDatasetSummaryDto, WidgetRequest, WidgetType } from '../models';

const WIDGET_TYPES: WidgetType[] = ['Kpi', 'LineChart', 'Table'];

@Component({
  selector: 'app-dashboard-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './dashboard-form.html',
  styleUrl: './dashboard-form.scss',
})
export class DashboardForm implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dashboardsService = inject(DashboardsService);
  private readonly formBuilder = inject(FormBuilder);

  readonly widgetTypes = WIDGET_TYPES;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly dashboardId = signal<string | null>(null);
  readonly widgets = signal<WidgetRequest[]>([]);
  readonly datasets = signal<AnalyticsDatasetSummaryDto[]>([]);
  readonly selectedMetrics = signal<string[]>([]);

  /** Só existe 1 dataset publicado nesta fase ("sales") — sem seletor de dataset na tela por não
   * haver nada para escolher ainda (E1.3/E1.4 da Fase 1). */
  readonly dataset = computed(() => this.datasets()[0] ?? null);

  readonly nameForm = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
  });

  readonly widgetForm = this.formBuilder.nonNullable.group({
    title: ['', Validators.required],
    type: this.formBuilder.nonNullable.control<WidgetType>('Kpi'),
    dimension: ['', Validators.required],
    unit: [''],
  });

  async ngOnInit(): Promise<void> {
    try {
      this.datasets.set(await this.dashboardsService.listDatasets());

      const id = this.route.snapshot.paramMap.get('id');
      if (id) {
        const dashboard = await this.dashboardsService.getById(id);
        this.dashboardId.set(dashboard.id);
        this.nameForm.controls.name.setValue(dashboard.name);
        this.widgets.set(
          dashboard.widgets.map((widget) => ({
            type: widget.type,
            title: widget.title,
            queryDefinition: { ...widget.queryDefinition, limit: widget.queryDefinition.limit ?? undefined },
            visualization: widget.visualization,
          })),
        );
      }
    } catch {
      this.errorMessage.set('Não foi possível carregar os dados necessários.');
    } finally {
      this.loading.set(false);
    }
  }

  toggleMetric(metric: string, checked: boolean): void {
    this.selectedMetrics.update((current) =>
      checked ? [...current, metric] : current.filter((m) => m !== metric),
    );
  }

  async createDraft(): Promise<void> {
    if (this.nameForm.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      const dashboard = await this.dashboardsService.create({ name: this.nameForm.getRawValue().name });
      this.dashboardId.set(dashboard.id);
    } catch (error) {
      this.errorMessage.set(this.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  addWidget(): void {
    if (this.widgetForm.invalid || this.selectedMetrics().length === 0) {
      this.errorMessage.set('Preencha o título, o tipo, a dimensão e selecione ao menos 1 métrica.');
      return;
    }

    const { title, type, dimension, unit } = this.widgetForm.getRawValue();
    const widget: WidgetRequest = {
      type,
      title,
      queryDefinition: {
        dataset: this.dataset()?.name ?? '',
        metrics: this.selectedMetrics(),
        dimensions: [dimension],
      },
      visualization: unit ? { unit } : undefined,
    };

    this.widgets.update((current) => [...current, widget]);
    this.widgetForm.reset({ title: '', type: 'Kpi', dimension: '', unit: '' });
    this.selectedMetrics.set([]);
    this.errorMessage.set(null);
  }

  removeWidget(index: number): void {
    this.widgets.update((current) => current.filter((_, i) => i !== index));
  }

  async saveDraft(): Promise<void> {
    await this.persist();
  }

  async publish(): Promise<void> {
    const id = await this.persist();
    if (!id) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.dashboardsService.publish(id);
      await this.router.navigate(['/dashboards', id]);
    } catch (error) {
      this.errorMessage.set(this.extractErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  private async persist(): Promise<string | null> {
    const id = this.dashboardId();
    if (!id || this.nameForm.invalid || this.saving()) {
      return null;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.dashboardsService.update(id, {
        name: this.nameForm.getRawValue().name,
        widgets: this.widgets(),
      });
      return id;
    } catch (error) {
      this.errorMessage.set(this.extractErrorMessage(error));
      return null;
    } finally {
      this.saving.set(false);
    }
  }

  private extractErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as ProblemDetails | undefined;
      return problem?.detail ?? 'Não foi possível salvar. Tente novamente.';
    }

    return 'Não foi possível salvar. Tente novamente.';
  }
}
