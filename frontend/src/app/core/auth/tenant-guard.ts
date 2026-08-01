import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth';

/** Exige token válido *e* tenant já selecionado — usado nas rotas de negócio (ex. dashboard). */
export const tenantGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  if (auth.requiresTenantSelection()) {
    return router.createUrlTree(['/select-tenant']);
  }

  return true;
};
