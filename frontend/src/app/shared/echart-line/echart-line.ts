import { AfterViewInit, Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

// Só os módulos usados (1 tipo de gráfico, sem interações avançadas) — o pacote `echarts` completo
// (todos os tipos de gráfico + componentes) infla o bundle sem necessidade nenhuma para este uso.
echarts.use([LineChart, GridComponent, TooltipComponent, CanvasRenderer]);

/**
 * Wrapper mínimo do Apache ECharts (docs/03-Stack-Tecnologica.md) para gráfico de linha — primeiro
 * uso de ECharts no frontend (E4.2, Fase 2). Sem `ngx-echarts`: a superfície usada aqui (1 tipo de
 * gráfico, sem interações avançadas) não justifica a dependência extra; `echarts.init`/`dispose`
 * direto sobre uma `<div>` é suficiente e evita atrelar a versão do Angular à de um wrapper de
 * terceiros.
 */
@Component({
  selector: 'app-echart-line',
  template: `<div #host class="h-full w-full" role="img" [attr.aria-label]="ariaLabel()"></div>`,
})
export class EchartLine implements AfterViewInit, OnDestroy {
  readonly host = viewChild.required<ElementRef<HTMLDivElement>>('host');

  readonly categories = input.required<string[]>();
  readonly values = input.required<number[]>();
  readonly seriesName = input('');
  readonly ariaLabel = input('Gráfico de linha');

  private chart: echarts.ECharts | null = null;

  constructor() {
    effect(() => {
      const categories = this.categories();
      const values = this.values();
      const seriesName = this.seriesName();
      this.chart?.setOption({
        xAxis: { type: 'category', data: categories },
        yAxis: { type: 'value' },
        series: [{ name: seriesName, type: 'line', data: values, smooth: true }],
      });
    });
  }

  ngAfterViewInit(): void {
    this.chart = echarts.init(this.host().nativeElement);
    this.chart.setOption({
      grid: { left: 40, right: 16, top: 16, bottom: 24 },
      xAxis: { type: 'category', data: this.categories() },
      yAxis: { type: 'value' },
      series: [{ name: this.seriesName(), type: 'line', data: this.values(), smooth: true }],
      tooltip: { trigger: 'axis' },
    });
  }

  ngOnDestroy(): void {
    this.chart?.dispose();
  }
}
