import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { DashboardList } from './dashboard-list';

describe('DashboardList', () => {
  let component: DashboardList;
  let fixture: ComponentFixture<DashboardList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardList],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardList);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
