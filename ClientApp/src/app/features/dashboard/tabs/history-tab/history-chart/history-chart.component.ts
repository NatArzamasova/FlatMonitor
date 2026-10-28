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

@Component({
  selector: 'app-history-chart',
  standalone: true,
  template: `
    <div style="position: relative; height: 600px; width: 100%;">
      <canvas #canvas></canvas>
    </div>
  `
})
export class HistoryChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) points: readonly HistoryPoint[] = [];
  @Input({ required: true }) title = '';

  @ViewChild('canvas', { static: true }) private canvasRef!: ElementRef<HTMLCanvasElement>;

  chart: Chart<'line', (number | null)[], string> | undefined;
  private viewReady = false;

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.render();
  }

  ngOnChanges(): void {
    if (this.viewReady) {
      this.render();
    }
  }

  ngOnDestroy(): void {
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

  private destroyChart(): void {
    this.chart?.destroy();
    this.chart = undefined;
  }
}
