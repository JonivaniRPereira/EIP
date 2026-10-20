import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { vi } from 'vitest';
import { Connectors } from './connectors';
import { Connector, ConnectorsService, SyncRun } from './connectors.service';
import { AuthService } from '../../core/auth/auth';

describe('Connectors', () => {
  const connector: Connector = {
    id: 'a',
    companyId: 'company',
    name: 'Clientes',
    baseUrl: 'http://localhost/sample',
    sourceEntity: 'customers',
    status: 'Active',
  };
  let service: {
    list: ReturnType<typeof vi.fn>;
    companies: ReturnType<typeof vi.fn>;
    register: ReturnType<typeof vi.fn>;
    runs: ReturnType<typeof vi.fn>;
    run: ReturnType<typeof vi.fn>;
    sync: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };
  let component: Connectors;
  let manage: boolean;
  beforeEach(() => {
    manage = true;
    service = {
      list: vi.fn().mockResolvedValue([]),
      companies: vi.fn().mockResolvedValue([]),
      register: vi.fn(),
      runs: vi.fn().mockResolvedValue([]),
      run: vi.fn(),
      sync: vi.fn(),
      update: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [Connectors],
      providers: [
        { provide: ConnectorsService, useValue: service },
        {
          provide: AuthService,
          useValue: {
            tenantId: () => 'tenant',
            hasPermission: (p: string) => p !== 'connector.manage' || manage,
          },
        },
      ],
    });
    component = TestBed.createComponent(Connectors).componentInstance;
  });
  afterEach(() => component.ngOnDestroy());
  it('saves configuration without changing company or source and keeps failed edits open', async () => {
    component.connectors.set([connector]);
    component.configure(connector);
    component.configurationForm.controls.name.setValue(' Novo nome ');
    service.update.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 400, error: { detail: 'Destino negado' } }),
    );
    await component.saveConfiguration();
    expect(component.configuring()).toBe(connector);
    expect(component.configurationError()).toBe('Destino negado');
    service.update.mockResolvedValue({ ...connector, name: 'Novo nome' });
    await component.saveConfiguration();
    expect(service.update).toHaveBeenLastCalledWith('a', {
      name: 'Novo nome',
      baseUrl: connector.baseUrl,
    });
    expect(component.connectors()[0].companyId).toBe(connector.companyId);
    expect(component.configuring()).toBeNull();
  });
  it('requires a valid past date and confirmation before reprocessing', async () => {
    component.openReprocess(connector);
    component.reprocessForm.controls.from.setValue('2999-01-01T12:00');
    component.prepareReprocess();
    expect(component.confirmedFrom()).toBeNull();
    component.reprocessForm.controls.from.setValue('2026-01-01T12:00');
    component.prepareReprocess();
    expect(service.sync).not.toHaveBeenCalled();
    service.sync.mockResolvedValue({ syncRunId: 'period-run' });
    await component.confirmReprocess();
    expect(service.sync).toHaveBeenCalledWith('a', new Date('2026-01-01T12:00').toISOString());
    expect(component.trackedRunId()).toBe('period-run');
  });
  it('loads older runs using the last displayed cursor and resets on refresh', async () => {
    const page = Array.from(
      { length: 21 },
      (_, i) =>
        ({ id: `${i}`, createdAt: `2026-10-19T12:00:${String(i).padStart(2, '0')}Z` }) as SyncRun,
    );
    service.runs
      .mockResolvedValueOnce(page)
      .mockResolvedValueOnce([page[20]])
      .mockResolvedValueOnce([]);
    await component.select(connector);
    expect(component.runs()).toHaveLength(20);
    expect(component.hasOlderRuns()).toBe(true);
    await component.loadOlderRuns();
    expect(service.runs).toHaveBeenLastCalledWith('a', {
      before: page[19].createdAt,
      beforeId: '19',
    });
    expect(component.runs()).toHaveLength(21);
    expect(component.browsingOlderRuns()).toBe(true);
    await component.refreshRuns();
    expect(component.browsingOlderRuns()).toBe(false);
    expect(component.runs()).toEqual([]);
  });
  it('keeps a stale response from replacing another selected integration', async () => {
    let resolveFirst!: (runs: SyncRun[]) => void;
    service.runs.mockImplementationOnce(
      () => new Promise<SyncRun[]>((resolve) => (resolveFirst = resolve)),
    );
    const first = component.select(connector);
    const second = { ...connector, id: 'b', name: 'Produtos' };
    await component.select(second);
    resolveFirst([{ id: 'old' } as SyncRun]);
    await first;
    expect(component.selected()?.id).toBe('b');
    expect(component.runs()).toEqual([]);
  });
  it('honors Retry-After and prevents another sync during the cooldown', async () => {
    service.sync.mockRejectedValue(
      new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '30' }) }),
    );
    await component.sync(connector);
    await component.sync(connector);
    expect(service.sync).toHaveBeenCalledTimes(1);
    expect(component.remaining('a')).toBeGreaterThan(0);
    expect(component.remaining('a')).toBeLessThanOrEqual(31);
    expect(component.error()).toContain('Limite');
  });
  it('retrieves the requested old run even when it is outside the recent history', async () => {
    component.connectors.set([connector]);
    const oldRun = {
      id: 'old-run',
      status: 'Succeeded',
      connectorInstanceId: connector.id,
    } as SyncRun;
    service.run.mockResolvedValue(oldRun);
    await component.trackRetry({ connectorId: connector.id, syncRunId: oldRun.id });
    expect(service.run).toHaveBeenCalledWith(connector.id, oldRun.id);
    expect(component.trackedRunId()).toBe(oldRun.id);
    expect(component.runs()).toEqual([oldRun]);
  });
  it('does not send writes for a read-only member', async () => {
    manage = false;
    await component.sync(connector);
    expect(service.sync).not.toHaveBeenCalled();
    expect(component.canManage()).toBe(false);
  });
  it('shows a permission error instead of treating a denied history as empty', async () => {
    service.runs.mockRejectedValue(new HttpErrorResponse({ status: 403 }));
    await component.select(connector);
    expect(component.runsError()).toContain('permissão');
  });
});
