import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/auth/auth';
import { AiProfile, AiSettingsService, AiSettingsView } from './ai-settings.service';

@Component({
  selector: 'app-ai-settings',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
  ],
  templateUrl: './ai-settings.html',
  styleUrl: './ai-settings.scss',
})
export class AiSettings implements OnInit {
  private readonly service = inject(AiSettingsService);
  readonly auth = inject(AuthService);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dirty = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  profiles: AiProfile[] = [];
  providers: string[] = [];
  activeProfileId = '';
  revision = 0;

  ngOnInit(): Promise<void> {
    return this.load();
  }

  async load(): Promise<void> {
    if (!this.auth.hasPermission('ai.settings.manage') || this.saving()) return;
    this.loading.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const [settings, providers] = await Promise.all([
        this.service.get(),
        this.service.providers(),
      ]);
      this.providers = providers;
      this.apply(settings);
    } catch {
      this.error.set('Não foi possível carregar as configurações. Tente novamente.');
    } finally {
      this.loading.set(false);
    }
  }

  changed(): void {
    this.dirty.set(true);
    this.success.set('');
  }

  renameProfile(index: number, id: string): void {
    if (this.activeProfileId === this.profiles[index].id) this.activeProfileId = id;
    this.profiles[index].id = id;
    this.changed();
  }

  addProfile(): void {
    if (this.profiles.length >= 20 || this.providers.length === 0 || this.saving()) return;
    this.profiles = [
      ...this.profiles,
      {
        id: `perfil-${crypto.randomUUID().slice(0, 8)}`,
        provider: this.providers[0],
        model: '',
        timeoutSeconds: 30,
        maxOutputTokens: 1024,
      },
    ];
    this.changed();
  }

  removeProfile(index: number): void {
    if (this.saving()) return;
    if (this.profiles[index]?.id === this.activeProfileId) this.activeProfileId = '';
    this.profiles = this.profiles.filter((_, position) => position !== index);
    this.changed();
  }

  async save(): Promise<void> {
    if (
      !this.auth.hasPermission('ai.settings.manage') ||
      this.saving() ||
      this.loading() ||
      !this.dirty()
    )
      return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const saved = await this.service.save({
        revision: this.revision,
        activeProfileId: this.activeProfileId || null,
        profiles: this.profiles.map((profile) => ({ ...profile, model: profile.model.trim() })),
      });
      this.apply(saved);
      this.success.set(
        'Configuração salva para esta organização. As chamadas de IA continuam desativadas.',
      );
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'Outra sessão alterou a configuração. Recarregue e revise suas alterações antes de salvar.'
          : error instanceof HttpErrorResponse && error.status === 400
            ? 'Confira os modelos, os identificadores únicos e os limites de cada perfil.'
            : 'Não foi possível salvar. Suas alterações foram mantidas para tentar novamente.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  providerName(provider: string): string {
    return (
      ({ openai: 'OpenAI', anthropic: 'Anthropic', google: 'Google' } as Record<string, string>)[
        provider
      ] ?? provider
    );
  }

  private apply(settings: AiSettingsView): void {
    this.profiles = settings.profiles.map((profile) => ({ ...profile }));
    this.activeProfileId = settings.activeProfileId ?? '';
    this.revision = settings.revision;
    this.dirty.set(false);
  }
}
