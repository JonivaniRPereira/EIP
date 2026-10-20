import { Component, OnInit, OnDestroy, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../core/auth/auth';
import {
  Company,
  Connector,
  ConnectorsService,
  HistoryCursor,
  SyncRun,
} from './connectors.service';
import { ConnectorFailures } from './connector-failures';

@Component({
  selector: 'app-connectors',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ConnectorFailures,
  ],
  templateUrl: './connectors.html',
  styleUrl: './connectors.scss',
})
export class Connectors implements OnInit, OnDestroy {
  private readonly service = inject(ConnectorsService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  readonly canView = computed(() => this.auth.hasPermission('connector.view'));
  readonly canManage = computed(() => this.auth.hasPermission('connector.manage'));
  readonly connectors = signal<Connector[]>([]);
  readonly companies = signal<Company[]>([]);
  readonly selected = signal<Connector | null>(null);
  readonly runs = signal<SyncRun[]>([]);
  readonly trackedRunId = signal<string | null>(null);
  readonly hasOlderRuns = signal(false);
  readonly olderRunsLoading = signal(false);
  readonly browsingOlderRuns = signal(false);
  private runsCursor: HistoryCursor | undefined;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly runsLoading = signal(false);
  readonly pending = signal<string[]>([]);
  readonly cooldowns = signal<Record<string, number>>({});
  readonly now = signal(Date.now());
  readonly error = signal<string | null>(null);
  readonly runsError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly configuring = signal<Connector | null>(null);
  readonly reprocessing = signal<Connector | null>(null);
  readonly configurationSaving = signal(false);
  readonly configurationError = signal<string | null>(null);
  readonly reprocessError = signal<string | null>(null);
  readonly confirmedFrom = signal<string | null>(null);
  readonly configurationForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    baseUrl: [
      '',
      [Validators.required, Validators.maxLength(2000), Validators.pattern(/^https?:\/\/\S+$/)],
    ],
  });
  readonly reprocessForm = this.fb.nonNullable.group({ from: ['', Validators.required] });
  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    companyId: ['', Validators.required],
    sourceEntity: ['customers', Validators.required],
    baseUrl: ['', [Validators.required, Validators.pattern(/^https?:\/\/\S+$/)]],
  });
  private timer?: ReturnType<typeof setInterval>;
  private destroyed = false;
  private runRequest = 0;

  async ngOnInit(): Promise<void> {
    await this.reload();
    let ticks = 0;
    if (this.destroyed) return;
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      if (
        ++ticks % 5 === 0 &&
        !this.runsLoading() &&
        !this.browsingOlderRuns() &&
        this.runs().some((r) => r.status === 'Pending' || r.status === 'Running')
      ) {
        void this.refreshRuns();
      }
    }, 1000);
  }
  ngOnDestroy(): void {
    this.destroyed = true;
    clearInterval(this.timer);
    this.runRequest++;
  }

  async reload(): Promise<void> {
    if (!this.canView() || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const connectors = await this.service.list();
      if (this.destroyed) return;
      this.connectors.set(connectors);
      const tenant = this.auth.tenantId();
      if (this.canManage() && tenant && this.auth.hasPermission('companies.view')) {
        this.companies.set(await this.service.companies(tenant));
        if (!this.form.controls.companyId.value && this.companies().length === 1)
          this.form.controls.companyId.setValue(this.companies()[0].id);
      }
    } catch (error) {
      if (!this.destroyed) this.error.set(this.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  async register(directive?: FormGroupDirective): Promise<void> {
    this.form.markAllAsTouched();
    if (!this.canManage() || this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const values = this.form.getRawValue();
      const connector = await this.service.register({
        ...values,
        name: values.name.trim(),
        baseUrl: values.baseUrl.trim(),
      });
      if (this.destroyed) return;
      this.connectors.update((items) => [...items, connector]);
      const next = { ...values, name: '', baseUrl: '' };
      if (directive) directive.resetForm(next);
      else this.form.reset(next);
      this.notice.set('Integração cadastrada. Agora você pode sincronizar.');
      await this.select(connector);
    } catch (error) {
      if (!this.destroyed) this.error.set(this.message(error));
    } finally {
      this.saving.set(false);
    }
  }
  async select(connector: Connector, runId: string | null = null): Promise<void> {
    this.selected.set(connector);
    this.trackedRunId.set(runId);
    this.hasOlderRuns.set(false);
    this.runsCursor = undefined;
    this.browsingOlderRuns.set(false);
    this.olderRunsLoading.set(false);
    this.runs.set([]);
    await this.refreshRuns();
  }
  async trackRetry(request: { connectorId: string; syncRunId: string }): Promise<void> {
    const connector = this.connectors().find((c) => c.id === request.connectorId);
    if (connector) {
      await this.select(connector, request.syncRunId);
    }
  }
  async refreshRuns(): Promise<void> {
    const connector = this.selected();
    if (!connector) return;
    const version = ++this.runRequest;
    this.browsingOlderRuns.set(false);
    this.olderRunsLoading.set(false);
    this.runsLoading.set(true);
    this.runsError.set(null);
    try {
      const page = await this.service.runs(connector.id);
      if (this.destroyed || version !== this.runRequest) return;
      let runs = page.slice(0, 20);
      this.hasOlderRuns.set(page.length > 20);
      const last = runs.at(-1);
      this.runsCursor = last ? { before: last.createdAt, beforeId: last.id } : undefined;
      const tracked = this.trackedRunId();
      if (tracked && !runs.some((run) => run.id === tracked)) {
        runs = [await this.service.run(connector.id, tracked), ...runs];
      }
      if (!this.destroyed && version === this.runRequest) this.runs.set(runs);
    } catch (error) {
      if (!this.destroyed && version === this.runRequest) this.runsError.set(this.message(error));
    } finally {
      if (!this.destroyed && version === this.runRequest) this.runsLoading.set(false);
    }
  }
  async loadOlderRuns(): Promise<void> {
    const connector = this.selected();
    if (
      !connector ||
      !this.runsCursor ||
      !this.hasOlderRuns() ||
      this.olderRunsLoading() ||
      this.runsLoading()
    )
      return;
    const version = ++this.runRequest;
    this.browsingOlderRuns.set(true);
    this.olderRunsLoading.set(true);
    this.runsError.set(null);
    try {
      const page = await this.service.runs(connector.id, this.runsCursor);
      if (this.destroyed || version !== this.runRequest) return;
      const items = page.slice(0, 20);
      const last = items.at(-1);
      this.runsCursor = last ? { before: last.createdAt, beforeId: last.id } : undefined;
      this.hasOlderRuns.set(page.length > 20);
      this.runs.update((current) => [
        ...current,
        ...items.filter((item) => !current.some((run) => run.id === item.id)),
      ]);
    } catch (error) {
      if (!this.destroyed && version === this.runRequest) this.runsError.set(this.message(error));
    } finally {
      if (!this.destroyed && version === this.runRequest) this.olderRunsLoading.set(false);
    }
  }
  remaining(id: string): number {
    return Math.max(0, Math.ceil(((this.cooldowns()[id] ?? 0) - this.now()) / 1000));
  }
  configure(connector: Connector): void {
    if (!this.canManage() || this.configurationSaving()) return;
    this.configuring.set(connector);
    this.configurationForm.reset({ name: connector.name, baseUrl: connector.baseUrl });
    this.configurationError.set(null);
  }
  async saveConfiguration(): Promise<void> {
    const connector = this.configuring();
    this.configurationForm.markAllAsTouched();
    if (
      !connector ||
      !this.canManage() ||
      this.configurationForm.invalid ||
      this.configurationSaving()
    )
      return;
    const value = this.configurationForm.getRawValue();
    if (!value.name.trim()) {
      this.configurationError.set('Informe um nome.');
      return;
    }
    this.configurationSaving.set(true);
    this.configurationError.set(null);
    try {
      const updated = await this.service.update(connector.id, {
        name: value.name.trim(),
        baseUrl: value.baseUrl.trim(),
      });
      if (this.destroyed) return;
      this.connectors.update((items) =>
        items.map((item) => (item.id === updated.id ? updated : item)),
      );
      if (this.selected()?.id === updated.id) this.selected.set(updated);
      this.configuring.set(null);
      this.notice.set('Configuração salva. As próximas execuções usarão esse endereço.');
    } catch (error) {
      if (!this.destroyed) this.configurationError.set(this.message(error));
    } finally {
      this.configurationSaving.set(false);
    }
  }
  openReprocess(connector: Connector): void {
    if (!this.canManage() || this.pending().includes(connector.id)) return;
    this.reprocessing.set(connector);
    this.reprocessForm.reset({ from: '' });
    this.confirmedFrom.set(null);
    this.reprocessError.set(null);
  }
  prepareReprocess(): void {
    this.reprocessForm.markAllAsTouched();
    const value = this.reprocessForm.controls.from.value;
    const timestamp = new Date(value).getTime();
    if (
      !this.canManage() ||
      this.reprocessForm.invalid ||
      !Number.isFinite(timestamp) ||
      timestamp > Date.now()
    ) {
      this.reprocessError.set('Informe uma data e hora inicial válida, anterior ao momento atual.');
      return;
    }
    this.reprocessError.set(null);
    this.confirmedFrom.set(new Date(timestamp).toISOString());
  }
  async confirmReprocess(): Promise<void> {
    const connector = this.reprocessing();
    const from = this.confirmedFrom();
    if (!connector || !from) return;
    await this.sync(connector, from);
  }
  async sync(connector: Connector, reprocessFrom?: string): Promise<void> {
    if (!this.canManage() || this.pending().includes(connector.id) || this.remaining(connector.id))
      return;
    this.pending.update((ids) => [...ids, connector.id]);
    this.error.set(null);
    this.notice.set(null);
    try {
      const request = reprocessFrom
        ? await this.service.sync(connector.id, reprocessFrom)
        : await this.service.sync(connector.id);
      if (this.destroyed) return;
      this.notice.set('Sincronização solicitada. Acompanhe a execução abaixo.');
      if (reprocessFrom) {
        this.reprocessing.set(null);
        this.confirmedFrom.set(null);
      }
      await this.select(connector, request.syncRunId);
    } catch (error) {
      if (this.destroyed) return;
      if (error instanceof HttpErrorResponse && error.status === 429) {
        const header = error.headers.get('Retry-After');
        const seconds = Number(header);
        const delay =
          header && Number.isFinite(seconds)
            ? seconds * 1000
            : Date.parse(header ?? '') - Date.now();
        const now = Date.now();
        this.now.set(now);
        this.cooldowns.update((values) => ({
          ...values,
          [connector.id]: now + (Number.isFinite(delay) && delay > 0 ? delay : 60000),
        }));
        this.error.set('Limite de sincronizações atingido. Aguarde o prazo indicado no botão.');
      } else this.error.set(this.message(error));
    } finally {
      this.pending.update((ids) => ids.filter((id) => id !== connector.id));
    }
  }
  entityLabel(entity: string): string {
    return (
      (
        { customers: 'Clientes', products: 'Produtos', 'sales-invoices': 'Vendas' } as Record<
          string,
          string
        >
      )[entity] ?? entity
    );
  }
  statusLabel(status: string): string {
    return (
      (
        {
          Pending: 'Na fila',
          Running: 'Em andamento',
          Succeeded: 'Concluída',
          Failed: 'Falhou',
          Active: 'Ativa',
          Inactive: 'Inativa',
        } as Record<string, string>
      )[status] ?? status
    );
  }
  private message(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 403) return 'Você não tem permissão para esta ação.';
      if (error.status === 404) return 'Integração não encontrada nesta organização.';
      if (error.status === 0)
        return 'Não foi possível conectar. Verifique a conexão e tente novamente.';
      if (error.status === 400 && typeof error.error?.detail === 'string')
        return error.error.detail;
    }
    return 'Não foi possível concluir a operação. Tente novamente.';
  }
}
