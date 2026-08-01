import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth';

/** Exige apenas um token válido — usado na tela de seleção de tenant, que roda antes de o tenant
 * estar definido. Rotas que precisam de tenant selecionado usam {@link tenantGuard}. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};
