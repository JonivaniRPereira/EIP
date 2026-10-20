import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth';
import {
  Connector,
  ConnectorsService,
  DlqEntry,
  HistoryCursor,
  QuarantineEntry,
} from './connectors.service';

interface RetryAction {
  kind: 'quarantine' | 'dlq';
  id: string;
  connectorId: string;
}

@Component({
  selector: 'app-connector-failures',
  imports: [DatePipe, MatButtonModule],
  templateUrl: './connector-failures.html',
  styleUrl: './connector-failures.scss',
})
export class ConnectorFailures implements OnInit, OnDestroy {
  private readonly service = inject(ConnectorsService);
  private readonly auth = inject(AuthService);
  readonly connectors = input<Connector[]>([]);
  readonly retried = output<{ connectorId: string; syncRunId: string }>();
  readonly canManage = computed(() => this.auth.hasPermission('connector.manage'));
  readonly entries = signal<QuarantineEntry[]>([]);
  readonly messages = signal<DlqEntry[]>([]);
  readonly filter = signal('');
  readonly showRequested = signal(false);
  readonly visibleEntries = computed(() =>
    this.entries().filter(
      (e) =>
        (!this.filter() || e.connectorInstanceId === this.filter()) &&
        (this.showRequested() || !e.resolvedAt),
    ),
  );
  readonly visibleMessages = computed(() =>
    this.messages().filter((e) => !this.filter() || e.connectorInstanceId === this.filter()),
  );
  readonly loading = signal(false);
  readonly hasOlderEntries = signal(false);
  readonly olderEntriesLoading = signal(false);
  private entriesCursor: HistoryCursor | undefined;
  readonly quarantineError = signal<string | null>(null);
  readonly dlqError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly action = signal<RetryAction | null>(null);
  readonly busy = signal(false);
  readonly cooldowns = signal<Record<string, number>>({});
  readonly now = signal(Date.now());
  private destroyed = false;
  private version = 0;
  private timer?: ReturnType<typeof setInterval>;

  async ngOnInit(): Promise<void> {
    if (!this.auth.hasPermission('connector.view')) return;
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
    await this.reload();
  }
  ngOnDestroy(): void {
    this.destroyed = true;
    this.version++;
    clearInterval(this.timer);
  }
  async reload(): Promise<void> {
    if (!this.auth.hasPermission('connector.view')) return;
    const version = ++this.version;
    this.loading.set(true);
    this.olderEntriesLoading.set(false);
    this.quarantineError.set(null);
    this.dlqError.set(null);
    const [quarantine, dlq] = await Promise.allSettled([
      this.service.quarantine({
        connectorInstanceId: this.filter() || undefined,
        includeRequested: this.showRequested(),
      }),
      this.service.dlq(),
    ]);
    if (this.destroyed || version !== this.version) return;
    if (quarantine.status === 'fulfilled') {
      const items = quarantine.value.slice(0, 20);
      this.entries.set(items);
      this.hasOlderEntries.set(quarantine.value.length > 20);
      const last = items.at(-1);
      this.entriesCursor = last ? { before: last.createdAt, beforeId: last.id } : undefined;
    } else {
      this.entries.set([]);
      this.hasOlderEntries.set(false);
      this.entriesCursor = undefined;
      this.quarantineError.set(this.message(quarantine.reason));
    }
    if (dlq.status === 'fulfilled') this.messages.set(dlq.value);
    else {
      this.messages.set([]);
      this.dlqError.set(this.message(dlq.reason));
    }
    this.loading.set(false);
  }
  name(id: string): string {
    return this.connectors().find((c) => c.id === id)?.name ?? 'Integração indisponível';
  }
  setFilter(event: Event): void {
    this.filter.set((event.target as HTMLSelectElement).value);
    this.action.set(null);
    void this.reload();
  }
  setRequested(): void {
    this.showRequested.update((value) => !value);
    this.action.set(null);
    void this.reload();
  }
  async loadOlderEntries(): Promise<void> {
    if (
      !this.entriesCursor ||
      !this.hasOlderEntries() ||
      this.loading() ||
      this.olderEntriesLoading()
    )
      return;
    const version = ++this.version;
    this.olderEntriesLoading.set(true);
    this.quarantineError.set(null);
    try {
      const page = await this.service.quarantine({
        connectorInstanceId: this.filter() || undefined,
        includeRequested: this.showRequested(),
        cursor: this.entriesCursor,
      });
      if (this.destroyed || version !== this.version) return;
      const items = page.slice(0, 20);
      const last = items.at(-1);
      this.entriesCursor = last ? { before: last.createdAt, beforeId: last.id } : undefined;
      this.hasOlderEntries.set(page.length > 20);
      this.entries.update((current) => [
        ...current,
        ...items.filter((item) => !current.some((entry) => entry.id === item.id)),
      ]);
    } catch (error) {
      if (!this.destroyed && version === this.version)
        this.quarantineError.set(this.message(error));
    } finally {
      if (!this.destroyed && version === this.version) this.olderEntriesLoading.set(false);
    }
  }
  remaining(id: string): number {
    return Math.max(0, Math.ceil(((this.cooldowns()[id] ?? 0) - this.now()) / 1000));
  }
  prepare(kind: 'quarantine' | 'dlq', id: string, connectorId: string): void {
    if (!this.canManage() || this.busy() || this.remaining(connectorId)) return;
    if (kind === 'quarantine' && this.entries().find((e) => e.id === id)?.resolvedAt) return;
    this.action.set({ kind, id, connectorId });
    this.actionError.set(null);
    this.notice.set(null);
  }
  async confirm(): Promise<void> {
    const action = this.action();
    if (!action || !this.canManage() || this.busy() || this.remaining(action.connectorId)) return;
    this.busy.set(true);
    this.actionError.set(null);
    try {
      let syncRunId = action.id;
      if (action.kind === 'quarantine')
        syncRunId = (await this.service.reprocess(action.id)).syncRunId;
      else await this.service.requeue(action.id);
      if (this.destroyed) return;
      this.action.set(null);
      this.notice.set(
        action.kind === 'quarantine'
          ? 'Nova sincronização integral solicitada. Confira a execução; isso ainda não comprova que o dado foi corrigido.'
          : 'Mensagem devolvida à fila de processamento. Acompanhe a execução para confirmar o resultado.',
      );
      this.retried.emit({ connectorId: action.connectorId, syncRunId });
      await this.reload();
    } catch (error) {
      if (this.destroyed) return;
      if (error instanceof HttpErrorResponse && error.status === 429) {
        const header = error.headers.get('Retry-After');
        const seconds = Number(header);
        const now = Date.now();
        const delay =
          header && Number.isFinite(seconds) ? seconds * 1000 : Date.parse(header ?? '') - now;
        this.now.set(now);
        this.cooldowns.update((values) => ({
          ...values,
          [action.connectorId]: now + (Number.isFinite(delay) && delay > 0 ? delay : 60000),
        }));
        this.actionError.set(
          'Limite de sincronizações atingido. Aguarde antes de tentar novamente.',
        );
      } else this.actionError.set(this.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  private message(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 403) return 'Você não tem permissão para esta operação.';
      if (error.status === 404)
        return 'Registro não encontrado. Atualize a lista antes de tentar novamente.';
      if (error.status === 409)
        return 'O reprocessamento deste registro já foi solicitado. Atualize a lista.';
      if (error.status === 0) return 'Não foi possível conectar. Tente atualizar a lista.';
    }
    return 'Não foi possível concluir a operação. Tente novamente.';
  }
}
