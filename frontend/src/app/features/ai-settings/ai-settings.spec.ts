import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { AiSettings } from './ai-settings';
import { AiSettingsService, AiSettingsView } from './ai-settings.service';
import { AuthService } from '../../core/auth/auth';

describe('AiSettings', () => {
  let component: AiSettings;
  let allowed: boolean;
  let service: {
    get: ReturnType<typeof vi.fn>;
    providers: ReturnType<typeof vi.fn>;
    save: ReturnType<typeof vi.fn>;
  };
  const snapshot = (): AiSettingsView => ({
    revision: 2,
    activeProfileId: 'primary',
    invocationEnabled: false,
    updatedAt: null,
    profiles: [
      {
        id: 'primary',
        provider: 'openai',
        model: 'demo-model',
        timeoutSeconds: 30,
        maxOutputTokens: 1024,
      },
    ],
  });
  beforeEach(() => {
    allowed = true;
    service = {
      get: vi.fn().mockResolvedValue(snapshot()),
      providers: vi.fn().mockResolvedValue(['openai', 'anthropic']),
      save: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [AiSettings],
      providers: [
        { provide: AiSettingsService, useValue: service },
        { provide: AuthService, useValue: { hasPermission: () => allowed } },
      ],
    });
    component = TestBed.createComponent(AiSettings).componentInstance;
  });
  it('does not load or save without administrative permission', async () => {
    allowed = false;
    await component.load();
    component.dirty.set(true);
    await component.save();
    expect(service.get).not.toHaveBeenCalled();
    expect(service.save).not.toHaveBeenCalled();
  });
  it('keeps the selected profile when its identifier is edited', async () => {
    await component.load();
    component.renameProfile(0, 'renamed');
    expect(component.activeProfileId).toBe('renamed');
    expect(component.dirty()).toBe(true);
  });
  it('sends the revision and profiles, without activation or tenant overrides', async () => {
    await component.load();
    component.profiles[0].model = ' another-model ';
    component.changed();
    service.save.mockResolvedValue({ ...snapshot(), revision: 3 });
    await component.save();
    expect(service.save).toHaveBeenCalledWith({
      revision: 2,
      activeProfileId: 'primary',
      profiles: [
        {
          id: 'primary',
          provider: 'openai',
          model: 'another-model',
          timeoutSeconds: 30,
          maxOutputTokens: 1024,
        },
      ],
    });
    expect(component.revision).toBe(3);
    expect(component.dirty()).toBe(false);
    expect(component.success()).toContain('continuam desativadas');
  });
  it('keeps edits when another session has saved a newer revision', async () => {
    await component.load();
    component.profiles[0].model = 'edited';
    component.changed();
    service.save.mockRejectedValue(new HttpErrorResponse({ status: 409 }));
    await component.save();
    expect(component.error()).toContain('Outra sessão');
    expect(component.profiles[0].model).toBe('edited');
    expect(component.dirty()).toBe(true);
    expect(component.saving()).toBe(false);
  });
  it('clears the preferred selection when that profile is removed', async () => {
    await component.load();
    component.removeProfile(0);
    expect(component.activeProfileId).toBe('');
    expect(component.profiles).toHaveLength(0);
    expect(component.dirty()).toBe(true);
  });
  it('preserves edits when saving fails and can reload the server state', async () => {
    await component.load();
    component.changed();
    service.save.mockRejectedValue(new HttpErrorResponse({ status: 503 }));
    await component.save();
    expect(component.error()).toContain('alterações foram mantidas');
    await component.load();
    expect(component.error()).toBe('');
    expect(component.dirty()).toBe(false);
  });
});
