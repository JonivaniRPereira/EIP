import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { firstValueFrom, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth';

interface TenantDto {
  id: string;
  name: string;
  slug: string;
  status: string;
}

/**
 * App shell (sidebar + topbar) com identidade JP Code Craft Solutions — construído
 * com Angular Material (já no stack, `docs/03`), nunca importando outra biblioteca de componentes.
 * Envolve toda rota autenticada+com-tenant (ver `app.routes.ts`); telas fora do tenant
 * (login/select-tenant/create-tenant) continuam no padrão de card centralizado, já que ainda não faz
 * sentido mostrar navegação de um tenant que não existe.
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);
  private readonly breakpoints = inject(BreakpointObserver);
  readonly compact = toSignal(
    this.breakpoints.observe('(max-width: 959px)').pipe(map((state) => state.matches)),
    { initialValue: this.breakpoints.isMatched('(max-width: 959px)') },
  );

  readonly tenantName = signal<string | null>(null);
  readonly sidenavOpened = signal(true);

  constructor() {
    effect(() => this.sidenavOpened.set(!this.compact()));
  }

  closeCompactMenu(): void {
    if (this.compact()) this.sidenavOpened.set(false);
  }

  async ngOnInit(): Promise<void> {
    const tenantId = this.auth.tenantId();
    if (!tenantId) {
      return;
    }

    try {
      const tenant = await firstValueFrom(this.http.get<TenantDto>(`${environment.apiBaseUrl}/tenants/${tenantId}`));
      this.tenantName.set(tenant.name);
    } catch {
      this.tenantName.set(null);
    }
  }

  toggleSidenav(): void {
    this.sidenavOpened.update((opened) => !opened);
  }

  userInitial(): string {
    return (this.auth.userEmail() ?? '?').charAt(0).toUpperCase();
  }

  logout(): void {
    this.auth.logout();
  }
}
