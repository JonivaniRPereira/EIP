import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { Router } from '@angular/router';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';

import { AuthService } from './auth';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(Router).navigateByUrl = () => Promise.resolve(true);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  const response = (token: string) => ({
    accessToken: token,
    refreshToken: `refresh-${token}`,
    accessTokenExpiresAtUtc: '2026-10-18T23:59:59Z',
    requiresTenantSelection: true,
    availableTenants: [],
  });

  async function login() {
    const pending = service.login({ email: 'test@test.local', password: 'Test!12345' });
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(response('a'));
    await pending;
  }

  it('shares a single refresh request between concurrent callers', async () => {
    await login();
    const first = service.refresh();
    const second = service.refresh();
    http.expectOne(`${environment.apiBaseUrl}/auth/refresh`).flush(response('b'));
    expect(await first).toBe(true);
    expect(await second).toBe(true);
    expect(service.accessToken).toBe('b');
  });

  it('revokes logout and does not restore a session from a late refresh response', async () => {
    await login();
    const pending = service.refresh();
    const refresh = http.expectOne(`${environment.apiBaseUrl}/auth/refresh`);
    service.logout();
    const logout = http.expectOne(`${environment.apiBaseUrl}/auth/logout`);
    expect(logout.request.body.refreshToken).toBe('refresh-a');
    logout.flush(null, { status: 204, statusText: 'No Content' });
    refresh.flush(response('b'));
    expect(await pending).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('starts unauthenticated when there is no stored session', () => {
    expect(service.isAuthenticated()).toBe(false);
  });
});
