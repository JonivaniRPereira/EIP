import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { DashboardsService } from '../dashboards.service';

import { DashboardForm } from './dashboard-form';

describe('DashboardForm', () => {
  let component: DashboardForm;
  let fixture: ComponentFixture<DashboardForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardForm],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({}) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('opens the persisted edit URL after creation so a reload can recover the draft', async () => {
    const id = '11111111-1111-1111-1111-111111111111';
    vi.spyOn(TestBed.inject(DashboardsService), 'create').mockResolvedValue({
      id, name: 'Visual', ownerId: id, status: 'Draft', widgets: [],
      createdAt: '2026-10-19T00:00:00Z', updatedAt: '2026-10-19T00:00:00Z',
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component.nameForm.setValue({ name: 'Visual' });
    await component.createDraft();
    expect(navigate).toHaveBeenCalledWith(['/dashboards', id, 'edit'], { replaceUrl: true });
    expect(component.dashboardId()).toBe(id);
  });
});
