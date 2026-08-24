import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { CreateTenant } from './create-tenant';

describe('CreateTenant', () => {
  let component: CreateTenant;
  let fixture: ComponentFixture<CreateTenant>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateTenant],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateTenant);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
