import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
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
import { buildHistoryChartConfig, HistoryPoint } from './history-chart.config';

// Register only what the line chart uses, so the rest of Chart.js can be tree-shaken.
Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Legend, Title, Tooltip);

/**
 * Line chart of the price history. Its size comes from the host element (600 px high by default; set `height` or
 * a flex size on `app-history-chart` to change it) and follows it when the host is resized.
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

  chart: Chart<'line', (number | null)[], string> | undefined;
  private viewReady = false;
  private resizeObserver: ResizeObserver | undefined;

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.render();
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

    this.chart = new Chart(canvas, buildHistoryChartConfig(this.points, this.title));
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
