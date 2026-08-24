import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

import { DashboardsService } from '../dashboards.service';
import { DashboardSummaryDto } from '../models';

@Component({
  selector: 'app-dashboard-list',
  imports: [RouterLink, DatePipe, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatTableModule],
  templateUrl: './dashboard-list.html',
  styleUrl: './dashboard-list.scss',
})
export class DashboardList implements OnInit {
  private readonly dashboardsService = inject(DashboardsService);

  readonly dashboards = signal<DashboardSummaryDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  readonly displayedColumns = ['name', 'status', 'updatedAt', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const dashboards = await this.dashboardsService.list();
      this.dashboards.set(dashboards);
    } catch {
      this.errorMessage.set('Não foi possível carregar os painéis.');
    } finally {
      this.loading.set(false);
    }
  }

  statusLabel(status: DashboardSummaryDto['status']): string {
    switch (status) {
      case 'Published':
        return 'Publicado';
      case 'Archived':
        return 'Arquivado';
      default:
        return 'Rascunho';
    }
  }
}
