import { TooltipItem } from 'chart.js';
import { buildHistoryChartConfig, formatHistoryDate, HistoryPoint, seriesColor } from './history-chart.config';

describe('history-chart.config', () => {
  const points: HistoryPoint[] = [
    { date: '2024-01-16', price: 13, label: 'Mitte' },
    { date: '2024-01-15', price: 12, label: 'Mitte' },
    { date: '2024-01-15', price: 20, label: 'Weststadt' },
    { date: '2024-01-17', price: 14, label: 'Mitte' }
  ];

  describe('buildHistoryChartConfig', () => {
    it('uses the sorted unique dates as x labels', () => {
      const config = buildHistoryChartConfig(points, 'Title');

      expect(config.data.labels).toEqual(['2024-01-15', '2024-01-16', '2024-01-17']);
    });

    it('creates one dataset per label in first-appearance order, with null for missing dates', () => {
      const config = buildHistoryChartConfig(points, 'Title');

      expect(config.data.datasets.map(dataset => dataset.label)).toEqual(['Mitte', 'Weststadt']);
      expect(config.data.datasets[0].data).toEqual([12, 13, 14]);
      expect(config.data.datasets[1].data).toEqual([20, null, null]);
    });

    it('enlarges the latest existing point of each line', () => {
      const config = buildHistoryChartConfig(points, 'Title');

      expect(config.data.datasets[0].pointRadius).toEqual([3, 3, 6]);
      expect(config.data.datasets[1].pointRadius).toEqual([6, 3, 3]);
    });

    it('colours the datasets from the palette', () => {
      const config = buildHistoryChartConfig(points, 'Title');

      expect(config.data.datasets[0].borderColor).toBe(seriesColor(0));
      expect(config.data.datasets[1].borderColor).toBe(seriesColor(1));
      expect(config.data.datasets[1].backgroundColor).toBe(seriesColor(1) + '20');
    });

    it('starts the price axis just below the minimum plotted price', () => {
      const y = buildHistoryChartConfig(
        [
          { date: '2024-01-15', price: 12.5, label: 'Mitte' },
          { date: '2024-01-16', price: 13.25, label: 'Mitte' }
        ],
        'Title'
      ).options!.scales!['y']!;

      expect((y as { beginAtZero?: boolean }).beginAtZero).toBe(false);
      expect(y.min as number).toBeLessThan(12.5);
      expect(y.min as number).toBeGreaterThanOrEqual(0);
    });

    it('formats x ticks as short dates', () => {
      const x = buildHistoryChartConfig(points, 'Title').options!.scales!['x']!;
      const callback = x.ticks!.callback as (value: number) => string;

      expect(callback(0)).toBe('15 Jan');
      expect(callback(2)).toBe('17 Jan');
    });

    it('shows tooltip prices rounded to whole euros', () => {
      const label = buildHistoryChartConfig(points, 'Title').options!.plugins!.tooltip!.callbacks!.label! as
        (context: TooltipItem<'line'>) => string;
      const context = (y: number) => ({ dataset: { label: 'Mitte' }, parsed: { y } }) as unknown as TooltipItem<'line'>;

      expect(label(context(12.49))).toBe('Mitte: €12/m²');
      expect(label(context(12.5))).toBe('Mitte: €13/m²');
    });

    it('uses the given title and keeps Chart.js from resizing the canvas itself', () => {
      const config = buildHistoryChartConfig(points, 'Price per m² Trend (by District)');

      expect(config.options!.plugins!.title!.text).toBe('Price per m² Trend (by District)');
      expect(config.options!.responsive).toBe(false);
      expect(config.options!.animation).toBe(false);
    });
  });

  describe('seriesColor', () => {
    it('cycles through the palette', () => {
      expect(seriesColor(0)).toBe('#0072B2');
      expect(seriesColor(28)).toBe(seriesColor(0));
    });
  });

  describe('formatHistoryDate', () => {
    it('formats ISO dates as day and short month in UTC', () => {
      expect(formatHistoryDate('2024-01-15')).toBe('15 Jan');
      expect(formatHistoryDate('2024-12-31')).toBe('31 Dec');
    });

    it('returns unparsable values unchanged', () => {
      expect(formatHistoryDate('not a date')).toBe('not a date');
    });
  });
});
