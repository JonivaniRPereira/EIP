import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService } from '../../core/auth/auth';
import { ProblemDetails } from '../../core/auth/models';

@Component({
  selector: 'app-select-tenant',
  imports: [MatButtonModule, MatCardModule, MatListModule, MatProgressSpinnerModule],
  templateUrl: './select-tenant.html',
  styleUrl: './select-tenant.scss',
})
export class SelectTenant {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly tenants = this.auth.availableTenants;
  readonly submittingTenantId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  async choose(tenantId: string): Promise<void> {
    if (this.submittingTenantId()) {
      return;
    }

    this.submittingTenantId.set(tenantId);
    this.errorMessage.set(null);

    try {
      await this.auth.selectTenant({ tenantId });
      await this.router.navigateByUrl('/dashboard');
    } catch (error) {
      const problem = error instanceof HttpErrorResponse ? (error.error as ProblemDetails | undefined) : undefined;
      this.errorMessage.set(problem?.detail ?? 'Não foi possível selecionar este tenant.');
    } finally {
      this.submittingTenantId.set(null);
    }
  }

  logout(): void {
    this.auth.logout();
  }
}
