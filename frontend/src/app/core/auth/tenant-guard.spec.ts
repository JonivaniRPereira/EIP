import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { CanActivateFn, provideRouter } from '@angular/router';

import { tenantGuard } from './tenant-guard';

describe('tenantGuard', () => {
  const executeGuard: CanActivateFn = (...guardParameters) =>
    TestBed.runInInjectionContext(() => tenantGuard(...guardParameters));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideRouter([])],
    });
  });

  it('should be created', () => {
    expect(executeGuard).toBeTruthy();
  });
});
