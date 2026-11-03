import { ActiveElement, Chart, ChartConfiguration, ChartEvent, TooltipItem } from 'chart.js';
import { roundPriceToEuro } from '../../../../../core/utils/aggregation.utils';

export interface HistoryPoint {
  date: string;
  price: number;
  label: string;
}

export type HistoryChartConfig = ChartConfiguration<'line', (number | null)[], string>;
type HistoryChart = Chart<'line', (number | null)[], string>;

/**
 * Colour-blind-friendly colours that stay distinct from each other and readable on white (no yellow, light grey or
 * black). Series beyond its length reuse colours; hovering a line highlights it, so repeats stay tellable apart.
 */
const SERIES_COLORS = [
  '#0072B2', '#E69F00', '#009E73', '#D55E00', '#CC79A7',
  '#56B4E9', '#332288', '#117733', '#882255', '#999933'
];

/** Alpha suffix for the lines that are not hovered. */
const DIMMED_ALPHA = '33';
const GRID_COLOR = 'rgba(0, 0, 0, 0.06)';

const DATE_FORMAT = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', timeZone: 'UTC' });
const PRICE_FORMAT = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 });

export function seriesColor(index: number): string {
  return SERIES_COLORS[index % SERIES_COLORS.length];
}

/** Each label's line colour: labels in first-appearance order take the palette in turn (as the datasets do). */
export function seriesColorsByLabel(points: readonly HistoryPoint[]): Map<string, string> {
  const colors = new Map<string, string>();
  for (const { label } of points) {
    if (!colors.has(label)) {
      colors.set(label, seriesColor(colors.size));
    }
  }
  return colors;
}

/** `2024-01-15` → `15 Jan`; anything unparsable is returned unchanged. */
export function formatHistoryDate(date: string): string {
  const parsedDate = new Date(`${date}T00:00:00Z`);
  return Number.isNaN(parsedDate.getTime()) ? date : DATE_FORMAT.format(parsedDate);
}

/**
 * Highlights one line and dims the others (`null` shows all normally). `colors` are the lines' own colours, by
 * dataset index. Redraws only when the highlighted line changes.
 */
export function highlightSeries(chart: HistoryChart, index: number | null, colors: readonly string[]): void {
  const state = chart as HistoryChart & { $highlightedSeries?: number | null };
  if ((state.$highlightedSeries ?? null) === index) {
    return;
  }
  state.$highlightedSeries = index;

  chart.data.datasets.forEach((dataset, datasetIndex) => {
    const dimmed = index !== null && datasetIndex !== index;
    const color = dimmed ? colors[datasetIndex] + DIMMED_ALPHA : colors[datasetIndex];
    dataset.borderColor = color;
    dataset.pointBackgroundColor = color;
    dataset.borderWidth = index === datasetIndex ? 3 : 2;
  });
  chart.update('none');
}

/**
 * One line per label over all dates (gaps as `null`), with the latest point of each line enlarged. The legend is
 * only shown when a line has no point on the latest date: the "Latest averages" table next to the chart names the
 * others, with the same colours.
 */
export function buildHistoryChartConfig(points: readonly HistoryPoint[], title: string): HistoryChartConfig {
  const dates = Array.from(new Set(points.map(point => point.date))).sort();
  const priceByLabelAndDate = new Map<string, Map<string, number>>();
  for (const { label, date, price } of points) {
    if (!priceByLabelAndDate.has(label)) {
      priceByLabelAndDate.set(label, new Map());
    }
    priceByLabelAndDate.get(label)!.set(date, price);
  }
  const colorByLabel = seriesColorsByLabel(points);

  const prices = points.map(point => point.price);
  const minPrice = Math.min(...prices);
  const maxPrice = Math.max(...prices);
  const pricePadding = Math.max((maxPrice - minPrice) * 0.1, minPrice * 0.05, 0.5);

  const datasets = Array.from(priceByLabelAndDate, ([label, priceByDate]) => {
    const color = colorByLabel.get(label)!;
    const data = dates.map(date => priceByDate.get(date) ?? null);
    const latestDataIndex = data.reduce<number>(
      (latestIndex, value, dataIndex) => value !== null ? dataIndex : latestIndex,
      -1
    );

    return {
      label,
      data,
      borderColor: color,
      backgroundColor: color + '20',
      borderWidth: 2,
      pointRadius: data.map((_, dataIndex) => dataIndex === latestDataIndex ? 6 : 3),
      pointHoverRadius: 7,
      pointBackgroundColor: color,
      tension: 0.2,
      fill: false
    };
  });
  const colors = datasets.map(dataset => dataset.borderColor);
  const someLineEndsEarly = datasets.some(dataset => dataset.data[dates.length - 1] === null);

  return {
    type: 'line',
    data: { labels: dates, datasets },
    options: {
      // The canvas is sized from its wrapper, and HistoryChartComponent resizes it when the wrapper changes.
      // Letting Chart.js own the sizing makes its resize observer fight the surrounding layout and lock the main thread.
      responsive: false,
      maintainAspectRatio: false,
      animation: false,
      // The tooltip shows only the line nearest to the mouse (the highlighted one), not every line of that date;
      // the mouse does not have to be exactly on a point.
      interaction: { mode: 'nearest', intersect: false },
      onHover: (event: ChartEvent, _elements: ActiveElement[], chart: Chart) => {
        if (datasets.length < 2) {
          return;
        }
        const nearest = event.native
          ? chart.getElementsAtEventForMode(event.native, 'nearest', { intersect: false }, false)[0]
          : undefined;
        highlightSeries(chart as HistoryChart, nearest?.datasetIndex ?? null, colors);
      },
      plugins: {
        legend: {
          display: someLineEndsEarly,
          position: 'bottom',
          labels: { usePointStyle: true, boxWidth: 8, boxHeight: 8 }
        },
        title: { display: true, text: title },
        tooltip: {
          callbacks: {
            label: (context: TooltipItem<'line'>) =>
              `${context.dataset.label}: €${roundPriceToEuro(Number(context.parsed.y))}/m²`
          }
        }
      },
      scales: {
        y: {
          beginAtZero: false,
          min: Math.max(0, minPrice - pricePadding),
          grid: { color: GRID_COLOR },
          border: { display: false },
          ticks: { callback: (value: string | number) => PRICE_FORMAT.format(Number(value)) },
          title: { display: true, text: 'Price per m²' }
        },
        x: {
          grid: { display: false },
          ticks: {
            callback: (value: string | number) => formatHistoryDate(dates[Number(value)])
          },
          title: { display: true, text: 'Date' }
        }
      }
    }
  };
}
