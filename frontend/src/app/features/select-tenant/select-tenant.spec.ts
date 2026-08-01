import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { SelectTenant } from './select-tenant';

describe('SelectTenant', () => {
  let component: SelectTenant;
  let fixture: ComponentFixture<SelectTenant>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelectTenant],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(SelectTenant);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
