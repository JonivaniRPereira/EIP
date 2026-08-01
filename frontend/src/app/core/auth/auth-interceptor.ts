import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth';

/**
 * Anexa o Bearer token em toda chamada para a API (nunca em requisições para outros hosts). Em um
 * 401 fora dos próprios endpoints de auth, tenta um refresh único e repete a requisição original —
 * se o refresh também falhar, o AuthService já desloga o usuário.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const token = auth.accessToken;
  const authorizedReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/refresh');
      if (error instanceof HttpErrorResponse && error.status === 401 && token && !isAuthEndpoint) {
        return from(auth.refresh()).pipe(
          switchMap((refreshed) => {
            if (!refreshed) {
              return throwError(() => error);
            }

            const retriedReq = req.clone({
              setHeaders: { Authorization: `Bearer ${auth.accessToken}` },
            });
            return next(retriedReq);
          }),
        );
      }

      return throwError(() => error);
    }),
  );
};
