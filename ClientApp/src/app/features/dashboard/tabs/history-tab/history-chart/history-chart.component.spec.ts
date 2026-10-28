import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Chart } from 'chart.js';
import { HistoryChartComponent } from './history-chart.component';
import { HistoryPoint } from './history-chart.config';

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
    expect(canvas().height).toBe(400);
    expect(canvas().width).toBeGreaterThan(0);
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
