import { AfterViewInit, Component, DestroyRef, ElementRef, Input, OnChanges, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  CategoryScale,
  Chart,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Title,
  Tooltip
} from 'chart.js';
import { buildHistoryChartConfig, HistoryPoint, readChartTheme } from './history-chart.config';
import { ThemeService } from '../../../../../core/services/theme.service';

// Register only what the line chart uses, so the rest of Chart.js can be tree-shaken.
Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Legend, Title, Tooltip);

/**
 * Line chart of the price history. Its size comes from the host element (600 px high by default; set `height` or
 * a flex size on `app-history-chart` to change it) and follows it when the host is resized. Text, grid and line colours
 * follow the light/dark theme: the chart is rebuilt when it changes.
 */
@Component({
  selector: 'app-history-chart',
  standalone: true,
  template: `
    <div class="chart-box">
      <canvas #canvas></canvas>
    </div>
  `,
  // The canvas is taken out of the flow, so it cannot change the size of the host it is measured from.
  styles: [`
    :host {
      display: block;
      position: relative;
      height: 600px;
      width: 100%;
    }
    .chart-box {
      position: absolute;
      inset: 0;
    }
  `]
})
export class HistoryChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) points: readonly HistoryPoint[] = [];
  @Input({ required: true }) title = '';

  @ViewChild('canvas', { static: true }) private canvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly themeService = inject(ThemeService);
  private readonly destroyRef = inject(DestroyRef);

  chart: Chart<'line', (number | null)[], string> | undefined;
  private viewReady = false;
  private resizeObserver: ResizeObserver | undefined;

  ngAfterViewInit(): void {
    this.viewReady = true;
    // Emits the current theme at once (first render), then on every change.
    this.themeService.theme$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.render());
    this.resizeObserver = new ResizeObserver(() => this.resizeToWrapper());
    this.resizeObserver.observe(this.canvasRef.nativeElement.parentElement!);
  }

  ngOnChanges(): void {
    if (this.viewReady) {
      this.render();
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.destroyChart();
  }

  private render(): void {
    this.destroyChart();
    if (this.points.length === 0) {
      return;
    }

    const canvas = this.canvasRef.nativeElement;
    const wrapper = canvas.parentElement;
    canvas.width = wrapper?.clientWidth || canvas.clientWidth;
    canvas.height = wrapper?.clientHeight || canvas.clientHeight;

    this.chart = new Chart(canvas, buildHistoryChartConfig(this.points, this.title, readChartTheme()));
  }

  /** Chart.js' own resizing is off (`responsive: false`), so the chart follows its wrapper from here. */
  private resizeToWrapper(): void {
    const wrapper = this.canvasRef.nativeElement.parentElement;
    if (!this.chart || !wrapper || wrapper.clientWidth === 0 || wrapper.clientHeight === 0) {
      return;
    }
    if (this.chart.width !== wrapper.clientWidth || this.chart.height !== wrapper.clientHeight) {
      this.chart.resize(wrapper.clientWidth, wrapper.clientHeight);
    }
  }

  private destroyChart(): void {
    this.chart?.destroy();
    this.chart = undefined;
  }
}
