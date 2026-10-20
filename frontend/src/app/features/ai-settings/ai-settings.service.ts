import { Service, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AiProfile {
  id: string;
  provider: string;
  model: string;
  timeoutSeconds: number;
  maxOutputTokens: number;
}
export interface AiSettingsView {
  revision: number;
  activeProfileId: string | null;
  profiles: AiProfile[];
  invocationEnabled: boolean;
  updatedAt: string | null;
}
export interface SaveAiSettings {
  revision: number;
  activeProfileId: string | null;
  profiles: AiProfile[];
}

@Service()
export class AiSettingsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/ai/settings`;
  get(): Promise<AiSettingsView> {
    return firstValueFrom(this.http.get<AiSettingsView>(this.url));
  }
  providers(): Promise<string[]> {
    return firstValueFrom(this.http.get<string[]>(`${this.url}/providers`));
  }
  save(settings: SaveAiSettings): Promise<AiSettingsView> {
    return firstValueFrom(this.http.put<AiSettingsView>(this.url, settings));
  }
}
