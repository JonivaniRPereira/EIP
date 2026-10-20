import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { vi } from 'vitest';
import { ConnectorFailures } from './connector-failures';
import { ConnectorsService, QuarantineEntry } from './connectors.service';
import { AuthService } from '../../core/auth/auth';

describe('ConnectorFailures', () => {
  let component: ConnectorFailures;
  let manage: boolean;
  let service: {
    quarantine: ReturnType<typeof vi.fn>;
    dlq: ReturnType<typeof vi.fn>;
    reprocess: ReturnType<typeof vi.fn>;
    requeue: ReturnType<typeof vi.fn>;
  };
  beforeEach(() => {
    manage = true;
    service = {
      quarantine: vi.fn().mockResolvedValue([]),
      dlq: vi.fn().mockResolvedValue([]),
      reprocess: vi.fn(),
      requeue: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [ConnectorFailures],
      providers: [
        { provide: ConnectorsService, useValue: service },
        {
          provide: AuthService,
          useValue: {
            hasPermission: (permission: string) => permission !== 'connector.manage' || manage,
          },
        },
      ],
    });
    component = TestBed.createComponent(ConnectorFailures).componentInstance;
  });
  afterEach(() => component.ngOnDestroy());
  it('paginates quarantine with server filters and resets the cursor when filters change', async () => {
    const page = Array.from(
      { length: 21 },
      (_, i) => ({ id: `${i}`, createdAt: '2026-10-19T12:00:00Z' }) as QuarantineEntry,
    );
    service.quarantine
      .mockResolvedValueOnce(page)
      .mockResolvedValueOnce([page[20]])
      .mockResolvedValueOnce([]);
    await component.reload();
    expect(component.entries()).toHaveLength(20);
    await component.loadOlderEntries();
    expect(service.quarantine).toHaveBeenLastCalledWith(
      expect.objectContaining({ cursor: { before: page[19].createdAt, beforeId: '19' } }),
    );
    expect(component.entries()).toHaveLength(21);
    component.setFilter({ target: { value: 'another' } } as unknown as Event);
    await Promise.resolve();
    expect(service.quarantine).toHaveBeenLastCalledWith(
      expect.objectContaining({ connectorInstanceId: 'another' }),
    );
    expect(service.quarantine.mock.lastCall?.[0].cursor).toBeUndefined();
  });
  it('requires the explicit confirmation before sending a retry', async () => {
    const retried = vi.fn();
    component.retried.subscribe(retried);
    service.reprocess.mockResolvedValue({ syncRunId: 'new' });
    component.prepare('quarantine', 'q', 'connector');
    expect(service.reprocess).not.toHaveBeenCalled();
    await component.confirm();
    expect(service.reprocess).toHaveBeenCalledWith('q');
    expect(component.notice()).toContain('não comprova');
    expect(retried).toHaveBeenCalledWith({ connectorId: 'connector', syncRunId: 'new' });
  });
  it('does not retry for a read-only session', async () => {
    manage = false;
    component.prepare('dlq', 'run', 'connector');
    await component.confirm();
    expect(service.requeue).not.toHaveBeenCalled();
  });
  it('keeps the successful section when the other API fails', async () => {
    service.quarantine.mockResolvedValue([{ id: 'q' }]);
    service.dlq.mockRejectedValue(new HttpErrorResponse({ status: 503 }));
    await component.reload();
    expect(component.entries()).toHaveLength(1);
    expect(component.dlqError()).toBeTruthy();
    expect(component.quarantineError()).toBeNull();
  });
  it('blocks repeated retries during Retry-After without marking the row resolved', async () => {
    const entry = { id: 'q', connectorInstanceId: 'c', resolvedAt: null } as QuarantineEntry;
    component.entries.set([entry]);
    service.reprocess.mockRejectedValue(
      new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '30' }) }),
    );
    component.prepare('quarantine', 'q', 'c');
    await component.confirm();
    await component.confirm();
    expect(service.reprocess).toHaveBeenCalledTimes(1);
    expect(component.remaining('c')).toBe(30);
    expect(component.entries()[0].resolvedAt).toBeNull();
  });
  it('hides requested rows by default and can reveal the audit history', () => {
    component.entries.set([
      { id: 'q', createdAt: '2026-10-19', resolvedAt: '2026-10-19' } as QuarantineEntry,
    ]);
    expect(component.visibleEntries()).toEqual([]);
    component.showRequested.set(true);
    expect(component.visibleEntries()).toHaveLength(1);
  });
});
