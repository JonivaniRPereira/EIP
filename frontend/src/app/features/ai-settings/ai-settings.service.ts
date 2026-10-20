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
export interface AiCredentialStatus {
  revision: number;
  protectionAvailable: boolean;
  configuredProfileIds: string[];
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
  credentials(): Promise<AiCredentialStatus> {
    return firstValueFrom(this.http.get<AiCredentialStatus>(`${this.url}/credentials`));
  }
  saveCredential(profileId: string, revision: number, secret: string): Promise<AiCredentialStatus> {
    return firstValueFrom(
      this.http.put<AiCredentialStatus>(
        `${this.url}/profiles/${encodeURIComponent(profileId)}/credential`,
        { revision, secret },
      ),
    );
  }
  removeCredential(profileId: string, revision: number): Promise<AiCredentialStatus> {
    return firstValueFrom(
      this.http.delete<AiCredentialStatus>(
        `${this.url}/profiles/${encodeURIComponent(profileId)}/credential`,
        { params: { revision } },
      ),
    );
  }
}
