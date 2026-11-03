import { TooltipItem } from 'chart.js';
import { Chart } from 'chart.js';
import { buildHistoryChartConfig, formatHistoryDate, highlightSeries, HistoryPoint, nearestSeriesHighlighter, seriesColor, seriesColorsByLabel } from './history-chart.config';

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

    it('draws gently curved lines and a tooltip for the nearest line only', () => {
      const config = buildHistoryChartConfig(points, 'Title');

      expect(config.data.datasets.every(dataset => dataset.tension === 0.2)).toBe(true);
      expect(config.options!.interaction).toEqual({ mode: 'nearest', intersect: false });
    });

    it('formats price ticks as whole euros and keeps only a subtle horizontal grid', () => {
      const scales = buildHistoryChartConfig(points, 'Title').options!.scales!;
      const callback = scales['y']!.ticks!.callback as (value: number) => string;

      expect(callback(4200)).toBe('€4,200');
      expect(scales['x']!.grid!.display).toBe(false);
      expect(scales['y']!.grid!.color).toBe('rgba(0, 0, 0, 0.06)');
    });

    it('hides the legend when every line reaches the latest date, and shows it at the bottom otherwise', () => {
      const legend = (chartPoints: HistoryPoint[]) => buildHistoryChartConfig(chartPoints, 'Title').options!.plugins!.legend!;

      expect(legend(points).display).toBe(true);
      expect(legend(points).position).toBe('bottom');
      expect(legend([...points, { date: '2024-01-17', price: 21, label: 'Weststadt' }]).display).toBe(false);
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
      expect(seriesColor(10)).toBe(seriesColor(0));
    });

    it('has no colours that are hard to see on white', () => {
      const palette = Array.from({ length: 10 }, (_, index) => seriesColor(index));

      expect(palette).not.toContain('#F0E442');
      expect(palette).not.toContain('#BBBBBB');
      expect(palette).not.toContain('#DDCC77');
      expect(palette).not.toContain('#000000');
      expect(new Set(palette).size).toBe(10);
    });
  });

  describe('seriesColorsByLabel', () => {
    it('gives the labels the palette in first-appearance order, like the datasets', () => {
      const colors = seriesColorsByLabel(points);

      expect(Array.from(colors)).toEqual([['Mitte', seriesColor(0)], ['Weststadt', seriesColor(1)]]);
      expect(buildHistoryChartConfig(points, 'Title').data.datasets.map(dataset => dataset.borderColor))
        .toEqual([colors.get('Mitte'), colors.get('Weststadt')]);
    });
  });

  describe('nearestSeriesHighlighter', () => {
    const colors = [seriesColor(0), seriesColor(1)];
    type AfterEvent = (chart: unknown, args: { event: { type: string; native: Event | null }; inChartArea?: boolean }) => void;

    const setUp = (nearestDataset: number | undefined) => {
      const config = buildHistoryChartConfig(points, 'Title');
      const chart = {
        data: config.data,
        update: jasmine.createSpy('update'),
        getElementsAtEventForMode: jasmine.createSpy('getElementsAtEventForMode')
          .and.returnValue(nearestDataset === undefined ? [] : [{ datasetIndex: nearestDataset }])
      };
      const afterEvent = nearestSeriesHighlighter(colors).afterEvent as unknown as AfterEvent;
      const borderColors = () => chart.data.datasets.map(dataset => dataset.borderColor);
      return { chart, afterEvent, borderColors, config };
    };
    const move = { type: 'mousemove', native: new MouseEvent('mousemove') };

    it('is part of the chart config', () => {
      const { config } = setUp(undefined);

      expect(config.plugins!.map(plugin => plugin.id)).toEqual(['nearestSeriesHighlighter']);
    });

    it('highlights the nearest line while the mouse is over the plot area', () => {
      const { chart, afterEvent, borderColors } = setUp(1);

      afterEvent(chart, { event: move, inChartArea: true });

      expect(borderColors()).toEqual([colors[0] + '33', colors[1]]);
    });

    it('shows all lines normally again when the mouse leaves the canvas', () => {
      const { chart, afterEvent, borderColors } = setUp(1);
      afterEvent(chart, { event: move, inChartArea: true });

      afterEvent(chart, { event: { type: 'mouseout', native: new MouseEvent('mouseout') }, inChartArea: false });

      expect(borderColors()).toEqual(colors);
    });

    it('shows all lines normally again when the mouse moves onto the axes', () => {
      const { chart, afterEvent, borderColors } = setUp(1);
      afterEvent(chart, { event: move, inChartArea: true });

      afterEvent(chart, { event: move, inChartArea: false });

      expect(borderColors()).toEqual(colors);
      expect(chart.getElementsAtEventForMode).toHaveBeenCalledTimes(1);
    });

    it('leaves a single line alone', () => {
      const { chart } = setUp(0);
      const afterEvent = nearestSeriesHighlighter([colors[0]]).afterEvent as unknown as AfterEvent;

      afterEvent(chart, { event: move, inChartArea: true });

      expect(chart.update).not.toHaveBeenCalled();
    });
  });

  describe('highlightSeries', () => {
    const fakeChart = () => {
      const config = buildHistoryChartConfig(points, 'Title');
      const chart = { data: config.data, update: jasmine.createSpy('update') };
      return chart as unknown as Chart<'line', (number | null)[], string> & { update: jasmine.Spy };
    };
    const colors = [seriesColor(0), seriesColor(1)];

    it('dims every line but the highlighted one', () => {
      const chart = fakeChart();

      highlightSeries(chart, 1, colors);

      expect(chart.data.datasets.map(dataset => dataset.borderColor)).toEqual([colors[0] + '33', colors[1]]);
      expect(chart.data.datasets.map(dataset => dataset.borderWidth)).toEqual([2, 3]);
      expect(chart.update).toHaveBeenCalledWith('none');
    });

    it('shows all lines normally again without a highlight', () => {
      const chart = fakeChart();
      highlightSeries(chart, 0, colors);

      highlightSeries(chart, null, colors);

      expect(chart.data.datasets.map(dataset => dataset.borderColor)).toEqual(colors);
      expect(chart.data.datasets.map(dataset => dataset.borderWidth)).toEqual([2, 2]);
    });

    it('redraws only when the highlighted line changes', () => {
      const chart = fakeChart();

      highlightSeries(chart, 0, colors);
      highlightSeries(chart, 0, colors);
      highlightSeries(chart, null, colors);
      highlightSeries(chart, null, colors);

      expect(chart.update).toHaveBeenCalledTimes(2);
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
