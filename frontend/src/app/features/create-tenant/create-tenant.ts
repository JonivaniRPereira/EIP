import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth';
import { ProblemDetails } from '../../core/auth/models';

interface CreateTenantResponse {
  tenantId: string;
  name: string;
  slug: string;
  companyId: string;
}

/**
 * Provisionamento próprio de tenant (Fase 3, E1.3) — fecha o beco sem saída que a tela de seleção
 * de tenant tinha até aqui quando `availableTenants` vinha vazio ("faça login novamente"). Depois de
 * criar, chama `auth.refresh()` (nunca um novo login) para o token já refletir a membership recém
 * criada — com exatamente 1 membership o tenant é selecionado automaticamente, sem etapa extra.
 */
@Component({
  selector: 'app-create-tenant',
  imports: [ReactiveFormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatProgressSpinnerModule],
  templateUrl: './create-tenant.html',
  styleUrl: './create-tenant.scss',
})
export class CreateTenant {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    slug: ['', Validators.required],
    countryCode: ['BR', Validators.required],
  });

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      await firstValueFrom(
        this.http.post<CreateTenantResponse>(`${environment.apiBaseUrl}/tenants`, this.form.getRawValue()),
      );

      await this.auth.refresh();
      await this.router.navigateByUrl(this.auth.requiresTenantSelection() ? '/select-tenant' : '/dashboard');
    } catch (error) {
      this.errorMessage.set(this.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  logout(): void {
    this.auth.logout();
  }

  private extractErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as ProblemDetails | undefined;
      return problem?.detail ?? 'Não foi possível criar a organização. Tente novamente.';
    }

    return 'Não foi possível criar a organização. Tente novamente.';
  }
}
