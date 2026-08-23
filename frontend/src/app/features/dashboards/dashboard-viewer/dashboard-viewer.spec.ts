import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { DashboardViewer } from './dashboard-viewer';

describe('DashboardViewer', () => {
  let component: DashboardViewer;
  let fixture: ComponentFixture<DashboardViewer>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardViewer],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'test-id' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardViewer);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
