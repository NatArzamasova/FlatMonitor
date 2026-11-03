import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Chart } from 'chart.js';
import { HistoryChartComponent } from './history-chart.component';
import { HistoryPoint, seriesColor } from './history-chart.config';
import { THEME_STORAGE_KEY, ThemeService } from '../../../../../core/services/theme.service';

describe('HistoryChartComponent', () => {
  let fixture: ComponentFixture<HistoryChartComponent>;
  let component: HistoryChartComponent;

  const points: HistoryPoint[] = [
    { date: '2024-01-15', price: 12, label: 'Mitte' },
    { date: '2024-01-16', price: 13, label: 'Mitte' },
    { date: '2024-01-15', price: 20, label: 'Weststadt' }
  ];

  const canvas = (): HTMLCanvasElement => fixture.nativeElement.querySelector('canvas');

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HistoryChartComponent] }).compileComponents();
    fixture = TestBed.createComponent(HistoryChartComponent);
    component = fixture.componentInstance;
  });

  it('draws a Chart.js line chart on its canvas sized from the wrapper', () => {
    fixture.componentRef.setInput('points', points);
    fixture.componentRef.setInput('title', 'Trend');
    fixture.detectChanges();

    const chart = Chart.getChart(canvas())!;
    expect(chart).toBe(component.chart as unknown as Chart);
    expect(chart.data.labels).toEqual(['2024-01-15', '2024-01-16']);
    expect(chart.data.datasets.map(dataset => dataset.label)).toEqual(['Mitte', 'Weststadt']);
    expect(canvas().height).toBe(600);
    expect(canvas().width).toBeGreaterThan(0);
  });

  describe('resizing', () => {
    // A stand-in ResizeObserver whose callback the test calls itself. The browser's one reports only when the page
    // renders a frame, and Chrome renders none in a tab it does not show (Karma's headless tab, at times): waiting for
    // it made this test time out now and then, locally and in CI.
    const realResizeObserver = window.ResizeObserver;
    let observed: Element[];
    let notifyResize: () => void;

    beforeEach(() => {
      observed = [];
      window.ResizeObserver = class {
        constructor(callback: ResizeObserverCallback) {
          notifyResize = () => callback([], this as unknown as ResizeObserver);
        }
        observe(target: Element) {
          observed.push(target);
        }
        unobserve() {}
        disconnect() {}
      } as unknown as typeof ResizeObserver;
    });

    afterEach(() => {
      fixture.destroy();
      window.ResizeObserver = realResizeObserver;
    });

    it('watches the canvas wrapper and resizes the same chart to it', () => {
      fixture.componentRef.setInput('points', points);
      fixture.componentRef.setInput('title', 'Trend');
      fixture.detectChanges();
      const chart = component.chart!;
      expect(observed).toEqual([canvas().parentElement!]);

      (fixture.nativeElement as HTMLElement).style.height = '400px';
      notifyResize();

      expect(component.chart).toBe(chart);
      expect(chart.height).toBe(400);
    });

    it('ignores a resize to nothing, e.g. while its tab is hidden', () => {
      fixture.componentRef.setInput('points', points);
      fixture.componentRef.setInput('title', 'Trend');
      fixture.detectChanges();
      const chart = component.chart!;
      const height = chart.height;
      spyOn(chart, 'resize').and.callThrough();

      (fixture.nativeElement as HTMLElement).style.display = 'none';
      notifyResize();

      expect(chart.resize).not.toHaveBeenCalled();
      expect(chart.height).toBe(height);
    });
  });

  it('replaces the chart when the inputs change', () => {
    fixture.componentRef.setInput('points', points);
    fixture.componentRef.setInput('title', 'By District');
    fixture.detectChanges();
    const firstChart = component.chart!;
    spyOn(firstChart, 'destroy').and.callThrough();

    fixture.componentRef.setInput('points', [{ date: '2024-01-15', price: 15, label: '2 rooms' }]);
    fixture.componentRef.setInput('title', 'By Room Number');
    fixture.detectChanges();

    expect(firstChart.destroy).toHaveBeenCalled();
    expect(component.chart).not.toBe(firstChart);
    expect(component.chart!.data.datasets.map(dataset => dataset.label)).toEqual(['2 rooms']);
    expect(component.chart!.options.plugins!.title!.text).toBe('By Room Number');
  });

  describe('theme', () => {
    afterEach(() => {
      localStorage.removeItem(THEME_STORAGE_KEY);
      document.documentElement.removeAttribute('data-bs-theme');
    });

    it('rebuilds the chart with the dark colours when the theme changes', () => {
      const themeService = TestBed.inject(ThemeService);
      themeService.setMode('light');
      fixture.componentRef.setInput('points', points);
      fixture.componentRef.setInput('title', 'Trend');
      fixture.detectChanges();
      const lightChart = component.chart!;
      const lightGrid = (lightChart.options.scales!['y']!.grid as { color: string }).color;

      themeService.setMode('dark');

      expect(component.chart).not.toBe(lightChart);
      expect(component.chart!.data.datasets[0].borderColor).toBe(seriesColor(0, true));
      expect((component.chart!.options.scales!['y']!.grid as { color: string }).color).not.toBe(lightGrid);
      expect(component.chart!.options.plugins!.title!.color)
        .toBe(getComputedStyle(document.documentElement).getPropertyValue('--bs-secondary-color').trim());
    });
  });

  it('does not draw a chart without points', () => {
    fixture.componentRef.setInput('points', []);
    fixture.componentRef.setInput('title', 'Trend');
    fixture.detectChanges();

    expect(component.chart).toBeUndefined();
    expect(Chart.getChart(canvas())).toBeUndefined();
  });

  it('destroys the chart when the component is destroyed', () => {
    fixture.componentRef.setInput('points', points);
    fixture.componentRef.setInput('title', 'Trend');
    fixture.detectChanges();
    const chart = component.chart!;
    spyOn(chart, 'destroy').and.callThrough();

    fixture.destroy();

    expect(chart.destroy).toHaveBeenCalled();
    expect(component.chart).toBeUndefined();
  });
});
