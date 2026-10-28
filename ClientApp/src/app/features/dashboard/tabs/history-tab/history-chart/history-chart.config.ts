import { ChartConfiguration, TooltipItem } from 'chart.js';
import { roundPriceToEuro } from '../../../../../core/utils/aggregation.utils';

export interface HistoryPoint {
  date: string;
  price: number;
  label: string;
}

export type HistoryChartConfig = ChartConfiguration<'line', (number | null)[], string>;

// Colour-blind-friendly palette; series beyond its length reuse colours.
const SERIES_COLORS = [
  '#0072B2', '#E69F00', '#009E73', '#D55E00', '#CC79A7', '#56B4E9',
  '#F0E442', '#000000', '#332288', '#88CCEE', '#44AA99', '#117733',
  '#999933', '#DDCC77', '#CC6677', '#882255', '#AA4499', '#661100',
  '#6699CC', '#AA4466', '#4477AA', '#228833', '#EE6677', '#BBBBBB',
  '#1B9E77', '#D95F02', '#7570B3', '#E7298A'
];

const DATE_FORMAT = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', timeZone: 'UTC' });

export function seriesColor(index: number): string {
  return SERIES_COLORS[index % SERIES_COLORS.length];
}

/** `2024-01-15` → `15 Jan`; anything unparsable is returned unchanged. */
export function formatHistoryDate(date: string): string {
  const parsedDate = new Date(`${date}T00:00:00Z`);
  return Number.isNaN(parsedDate.getTime()) ? date : DATE_FORMAT.format(parsedDate);
}

/** One line per label over all dates (gaps as `null`), with the latest point of each line enlarged. */
export function buildHistoryChartConfig(points: readonly HistoryPoint[], title: string): HistoryChartConfig {
  const dates = Array.from(new Set(points.map(point => point.date))).sort();
  const priceByLabelAndDate = new Map<string, Map<string, number>>();
  for (const { label, date, price } of points) {
    if (!priceByLabelAndDate.has(label)) {
      priceByLabelAndDate.set(label, new Map());
    }
    priceByLabelAndDate.get(label)!.set(date, price);
  }

  const prices = points.map(point => point.price);
  const minPrice = Math.min(...prices);
  const maxPrice = Math.max(...prices);
  const pricePadding = Math.max((maxPrice - minPrice) * 0.1, minPrice * 0.05, 0.5);

  const datasets = Array.from(priceByLabelAndDate, ([label, priceByDate], index) => {
    const color = seriesColor(index);
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
      tension: 0.4,
      fill: false
    };
  });

  return {
    type: 'line',
    data: { labels: dates, datasets },
    options: {
      // The canvas is sized from its wrapper. Letting Chart.js own the sizing makes its resize
      // observer fight the surrounding layout and lock the main thread.
      responsive: false,
      maintainAspectRatio: false,
      animation: false,
      plugins: {
        legend: { display: true, position: 'top' },
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
          title: { display: true, text: 'Price per m² (€)' }
        },
        x: {
          ticks: {
            callback: (value: string | number) => formatHistoryDate(dates[Number(value)])
          },
          title: { display: true, text: 'Date' }
        }
      }
    }
  };
}
