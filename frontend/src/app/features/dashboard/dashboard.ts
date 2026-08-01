import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth';

interface TenantDto {
  id: string;
  name: string;
  slug: string;
  status: string;
}

@Component({
  selector: 'app-dashboard',
  imports: [MatButtonModule, MatCardModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly tenant = signal<TenantDto | null>(null);
  readonly loading = signal(true);

  async ngOnInit(): Promise<void> {
    const tenantId = this.auth.tenantId();
    if (!tenantId) {
      this.loading.set(false);
      return;
    }

    try {
      const tenant = await firstValueFrom(
        this.http.get<TenantDto>(`${environment.apiBaseUrl}/tenants/${tenantId}`),
      );
      this.tenant.set(tenant);
    } finally {
      this.loading.set(false);
    }
  }

  logout(): void {
    this.auth.logout();
  }
}
