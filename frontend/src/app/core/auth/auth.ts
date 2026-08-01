import { Service, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest, SelectTenantRequest, TenantOption } from './models';

interface StoredSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
}

const STORAGE_KEY = 'eip.session';

/**
 * Estado de autenticação do cliente. Guarda o par de tokens no localStorage — suficiente para a
 * Fase 0 (docs/roadmap/fase-0-backlog.md, épico E6); armazenamento mais seguro (ex. cookie HttpOnly
 * emitido pelo Gateway) fica para quando houver um endurecimento de segurança dedicado do frontend.
 *
 * A lista de tenants disponíveis (`availableTenants`) só existe em memória, populada pela resposta
 * de login/register/refresh — não sobrevive a um F5 no meio do fluxo de seleção de tenant; nesse
 * caso o usuário simplesmente refaz o login (simplificação aceita para o MVP).
 */
@Service()
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(this.readStoredSession());
  private readonly pendingTenantOptions = signal<TenantOption[]>([]);

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly availableTenants = computed(() => this.pendingTenantOptions());
  readonly tenantId = computed(() => this.decodeClaim(this.session()?.accessToken, 'tenant_id'));
  readonly requiresTenantSelection = computed(() => this.isAuthenticated() && this.tenantId() === null);

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  async login(request: LoginRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, request),
    );
    this.applyAuthResponse(response);
  }

  async register(request: RegisterRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/register`, request),
    );
    this.applyAuthResponse(response);
  }

  async selectTenant(request: SelectTenantRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/select-tenant`, request),
    );
    this.applyAuthResponse(response);
  }

  /** Usado pelo interceptor quando o access token expira — nunca chamado diretamente pelas telas. */
  async refresh(): Promise<boolean> {
    const current = this.session();
    if (!current) {
      return false;
    }

    try {
      const response = await firstValueFrom(
        this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/refresh`, {
          refreshToken: current.refreshToken,
        }),
      );
      this.applyAuthResponse(response);
      return true;
    } catch {
      this.logout();
      return false;
    }
  }

  logout(): void {
    this.session.set(null);
    this.pendingTenantOptions.set([]);
    localStorage.removeItem(STORAGE_KEY);
    void this.router.navigateByUrl('/login');
  }

  private applyAuthResponse(response: AuthResponse): void {
    const stored: StoredSession = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
    };

    this.session.set(stored);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.pendingTenantOptions.set(response.requiresTenantSelection ? response.availableTenants : []);
  }

  private readStoredSession(): StoredSession | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as StoredSession;
    } catch {
      return null;
    }
  }

  private decodeClaim(accessToken: string | undefined, claim: string): string | null {
    if (!accessToken) {
      return null;
    }

    try {
      const payloadSegment = accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const payload = JSON.parse(atob(payloadSegment)) as Record<string, unknown>;
      return (payload[claim] as string | undefined) ?? null;
    } catch {
      return null;
    }
  }
}
