import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NEVER, of, Subject, throwError } from 'rxjs';
import { HistoryTabComponent } from './history-tab.component';
import { HistoryChartComponent } from './history-chart/history-chart.component';
import { FlatsService } from '../../../../core/services/flats.service';
import { DataReloadService } from '../../../../core/services/data-reload.service';
import { ScrapeStatusService } from '../../../../core/services/scrape-status.service';
import { apiResult } from '../../../../core/testing/api-response.testing';
import { ApiResponse } from '../../../../core/models/api-response.model';
import { FlatHistory } from '../../../../core/models/flat-history.model';
import { FilterPresetsService } from '../../../../core/services/filter-presets.service';
import {
  createFilterPresetsServiceStub,
  filterPreset,
  FilterPresetsServiceStub
} from '../../../../core/testing/filter-presets.testing';

describe('HistoryTabComponent', () => {
  let component: HistoryTabComponent;
  let fixture: ComponentFixture<HistoryTabComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;
  let presets: FilterPresetsServiceStub;
  let reload$: Subject<void>;

  const mockHistoryResponse: ApiResponse<FlatHistory[]> = {
    success: true,
    data: [
      {
        scrapingDate: '2024-01-15',
        district: 'Mitte',
        roomsCount: 2,
        avgPricePerMeter: 12.50,
        count: 5
      },
      {
        scrapingDate: '2024-01-16',
        district: 'Mitte',
        roomsCount: 2,
        avgPricePerMeter: 12.75,
        count: 6
      },
      {
        scrapingDate: '2024-01-15',
        district: 'Charlottenburg',
        roomsCount: 3,
        avgPricePerMeter: 11.20,
        count: 4
      }
    ],
    message: 'Success'
  };

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getHistory'
    ]);
    presets = createFilterPresetsServiceStub();
    reload$ = new Subject<void>();

    await TestBed.configureTestingModule({
      imports: [HistoryTabComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy },
        { provide: FilterPresetsService, useValue: presets.service },
        { provide: DataReloadService, useValue: { reload$ } },
        // Read by the empty state's "Start a scrape" button.
        { provide: ScrapeStatusService, useValue: jasmine.createSpyObj('ScrapeStatusService', ['triggerScrape'], { status$: NEVER }) }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    fixture = TestBed.createComponent(HistoryTabComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    if (fixture) {
      fixture.destroy();
    }
  });

  describe('Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.historyData).toEqual([]);
      expect(component.chartData).toEqual([]);
      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.groupBy).toBe('district');
    });

    it('should load history data on init', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(flatsService.getHistory).toHaveBeenCalled();
      expect(component.historyData.length).toBe(3);
    });
  });

  describe('loadHistory', () => {
    it('should load and display history data', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      component.loadHistory();

      expect(component.loading).toBe(false);
      expect(component.error).toBeNull();
      expect(component.historyData.length).toBe(3);
    });

    it('should handle API response failure', () => {
      const failureResponse: ApiResponse<FlatHistory[]> = {
        success: false,
        message: 'Failed to load history'
      };
      flatsService.getHistory.and.returnValue(apiResult(failureResponse));

      component.loadHistory();

      expect(component.error).toBe('Failed to load history');
      expect(component.loading).toBe(false);
      expect(component.historyData).toEqual([]);
    });

    it('should handle HTTP errors', () => {
      const error = new Error('Network error');
      flatsService.getHistory.and.returnValue(throwError(() => error));

      component.loadHistory();

      expect(component.error).toContain('Error loading history');
      expect(component.loading).toBe(false);
    });

  });

  describe('History Grouping', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      component.loadHistory();
    });

    it('should group by district by default', () => {
      expect(component.groupBy).toBe('district');
    });

    it('should allow grouping by rooms', () => {
      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.groupBy).toBe('rooms');
      expect(component.chartData.length).toBeGreaterThan(0);
    });

    it('should update chart data when group by changes', () => {
      const initialChartDataLength = component.chartData.length;

      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.chartData.length).toBeGreaterThanOrEqual(initialChartDataLength);
    });
  });

  describe('History Chart Data', () => {
    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      component.loadHistory();
    });

    it('should include all series in the chart data', () => {
      component.groupBy = 'district';

      const mitteDataCount = component.chartData.filter(d => d.label === 'Mitte').length;
      const charlottenburgDataCount = component.chartData.filter(d => d.label === 'Charlottenburg').length;
      expect(mitteDataCount).toBeGreaterThan(0);
      expect(charlottenburgDataCount).toBeGreaterThan(0);
    });

    it('should calculate district history as a flat-count-weighted average across room groups', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 4000, count: 5 },
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 6, avgPricePerMeter: 8756, count: 1 }
      ];
      component.groupBy = 'district';
      (component as any).updateChartData();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4793, label: 'Mitte' }
      ]);
    });

    it('should skip rows with zero flats and rows without a date, and label missing districts as Unknown', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', avgPricePerMeter: 4000, count: 1 },
        { scrapingDate: '2024-01-15', district: 'Mitte', avgPricePerMeter: 9999, count: 0 },
        { scrapingDate: null, district: 'Mitte', avgPricePerMeter: 9999, count: 5 },
        { scrapingDate: '2024-01-15', district: null, avgPricePerMeter: 3000, count: 2 }
      ];
      component.groupBy = 'district';
      (component as any).updateChartData();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4000, label: 'Mitte' },
        { date: '2024-01-15', price: 3000, label: 'Unknown' }
      ]);
    });

    it('should calculate room history as a flat-count-weighted average across districts', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 4000, count: 5 },
        { scrapingDate: '2024-01-15', district: 'Charlottenburg', roomsCount: 2, avgPricePerMeter: 8756, count: 1 }
      ];
      component.groupBy = 'rooms';
      (component as any).updateChartData();

      expect(component.chartData).toEqual([
        { date: '2024-01-15', price: 4793, label: '2 rooms' }
      ]);
    });

    it('shows the latest district averages sorted by price descending', () => {
      component.historyData = [
        { scrapingDate: '2024-01-15', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 2000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 3000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'West', roomsCount: 3, avgPricePerMeter: 5000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'East', roomsCount: 3, avgPricePerMeter: 4000, count: 1 }
      ];
      component.groupBy = 'district';
      (component as any).updateChartData();

      expect(component.latestAverageDate).toBe('2024-01-16');
      expect(component.latestAverageData).toEqual([
        { label: 'West', price: 5000 },
        { label: 'East', price: 4000 },
        { label: 'Mitte', price: 3000 }
      ]);
    });

    it('shows the latest room averages sorted by price descending when grouped by rooms', () => {
      component.historyData = [
        { scrapingDate: '2024-01-16', district: 'Mitte', roomsCount: 2, avgPricePerMeter: 3000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'West', roomsCount: 2, avgPricePerMeter: 5000, count: 1 },
        { scrapingDate: '2024-01-16', district: 'East', roomsCount: 4, avgPricePerMeter: 4000, count: 1 }
      ];
      component.groupBy = 'rooms';
      (component as any).updateChartData();

      expect(component.latestAverageData).toEqual([
        { label: '2 rooms', price: 4000 },
        { label: '4 rooms', price: 4000 }
      ]);
    });
  });

  describe('Latest averages table', () => {
    it('renders the latest date and its averages sorted by price', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      const heading = fixture.nativeElement.querySelector('h2') as HTMLElement;
      const rows = Array.from(fixture.nativeElement.querySelectorAll('table tbody tr')) as HTMLElement[];
      expect(heading.textContent).toContain('Latest averages (2024-01-16)');
      expect(rows.map(row => Array.from(row.querySelectorAll('td')).map(cell => cell.textContent!.trim())))
        .toEqual([['Mitte', '€13']]);
    });

    it('computes the latest averages when the data changes, not on every change detection', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      const latest = component.latestAverageData;

      fixture.detectChanges();

      expect(component.latestAverageData).toBe(latest);

      component.groupBy = 'rooms';
      component.onGroupByChange();

      expect(component.latestAverageData).not.toBe(latest);
      expect(component.latestAverageData).toEqual([{ label: '2 rooms', price: 13 }]);
    });
  });

  describe('Spinner and alerts (DOM)', () => {
    it('shows a loading skeleton while history is loading', () => {
      flatsService.getHistory.and.returnValue(NEVER);
      fixture.detectChanges();

      const skeleton = fixture.nativeElement.querySelector('app-loading-skeleton [role=\"status\"]') as HTMLElement;
      expect(skeleton).toBeTruthy();
      expect(skeleton.textContent).toContain('Loading…');
      expect(fixture.nativeElement.querySelector('app-empty-state')).toBeNull();
    });

    it('shows load errors in an alert that can be dismissed', () => {
      flatsService.getHistory.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('.alert-danger') as HTMLElement;
      expect(alert.textContent).toContain('Error loading history: Network error');

      (alert.querySelector('.btn-close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(component.error).toBeNull();
      expect(fixture.nativeElement.querySelector('.alert-danger')).toBeNull();
    });
  });

  describe('Chart', () => {
    const chartComponent = (): HistoryChartComponent | undefined =>
      fixture.debugElement.query(By.directive(HistoryChartComponent))?.componentInstance;

    it('passes the grouped series and title to the chart as soon as history data is shown', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(chartComponent()!.points).toBe(component.chartData);
      expect(chartComponent()!.title).toBe('Price per m² Trend (by District)');
      expect(chartComponent()!.chart).toBeDefined();
    });

    it('passes new series and title when the grouping changes', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      component.groupBy = 'rooms';
      component.onGroupByChange();
      fixture.detectChanges();

      expect(chartComponent()!.title).toBe('Price per m² Trend (by Rooms)');
      expect(chartComponent()!.points.map(point => point.label)).toContain('2 rooms');
    });

    it('does not show a chart when there is no history data', () => {
      flatsService.getHistory.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));

      fixture.detectChanges();

      expect(chartComponent()).toBeUndefined();
    });
  });

  describe('District filter and presets', () => {
    const labels = () => [...new Set(component.chartData.map(point => point.label))].sort();

    it('offers the districts from the history data', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(component.availableDistricts).toEqual(['Charlottenburg', 'Mitte']);
    });

    it('shows all districts when nothing is selected', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(labels()).toEqual(['Charlottenburg', 'Mitte']);
    });

    it('filters the chart and the latest averages by the selected districts', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      component.selectedDistricts = ['Mitte'];
      component.onDistrictFilterChange();

      expect(labels()).toEqual(['Mitte']);
      expect(component.latestAverageData.map(item => item.label)).toEqual(['Mitte']);
    });

    it('also narrows the room-number grouping', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      component.groupBy = 'rooms';
      component.selectedDistricts = ['Charlottenburg'];

      component.onDistrictFilterChange();

      expect(labels()).toEqual(['3 rooms']);
    });

    it('applies the active preset when the tab opens', () => {
      presets.activePreset$.next(filterPreset(1, 'Center', ['Mitte']));
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(component.selectedDistricts).toEqual(['Mitte']);
      expect(labels()).toEqual(['Mitte']);
    });

    it('applies a preset chosen later', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      presets.activePreset$.next(filterPreset(2, 'West', ['Charlottenburg']));

      expect(labels()).toEqual(['Charlottenburg']);
    });

    it('shows all districts when the active preset becomes null', () => {
      presets.activePreset$.next(filterPreset(1, 'Center', ['Mitte']));
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      presets.activePreset$.next(null);

      expect(component.selectedDistricts).toEqual([]);
      expect(labels()).toEqual(['Charlottenburg', 'Mitte']);
    });

    it('offers preset districts without history, and says when nothing matches', () => {
      presets.activePreset$.next(filterPreset(3, 'Far', ['Durlach']));
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(component.availableDistricts).toEqual(['Charlottenburg', 'Durlach', 'Mitte']);
      expect(component.chartData).toEqual([]);
      expect(fixture.nativeElement.textContent).toContain('No history data for the selected districts.');
      expect(fixture.debugElement.query(By.directive(HistoryChartComponent))).toBeNull();
    });

    it('renders the district filter and the preset bar', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));

      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('#historyDistrictSelect')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-filter-preset-bar select').id).toBe('historyPresetSelect');
    });
  });

  describe('Filter layout', () => {
    const row = (name: string): HTMLElement => fixture.nativeElement.querySelector(`[data-filter-row="${name}"]`);
    const rowNames = (): string[] =>
      Array.from(fixture.nativeElement.querySelectorAll('[data-filter-row]') as NodeListOf<HTMLElement>)
        .map(element => element.dataset['filterRow']!);
    /** Direct children of a row, by tag name or by the id of the control inside. */
    const rowItems = (name: string): string[] =>
      Array.from(row(name).children).map(child =>
        child.tagName.toLowerCase().startsWith('app-')
          ? `${child.tagName.toLowerCase()}#${child.querySelector('select, input, button[id]')?.id ?? ''}`
          : child.querySelector('select, input')?.id ?? child.tagName.toLowerCase());

    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
    });

    it('puts districts + preset and grouping on separate rows', () => {
      expect(rowNames()).toEqual(['districts', 'grouping']);
      expect(rowItems('districts')).toEqual([
        'app-multi-select-filter#historyDistrictSelect',
        'app-filter-preset-bar#historyPresetSelect'
      ]);
      expect(row('grouping').querySelector('#groupByDistrictRadio')).not.toBeNull();
      expect(row('grouping').querySelector('#groupByRoomsRadio')).not.toBeNull();
      const group = row('grouping').querySelector('[role="radiogroup"]')!;
      expect(fixture.nativeElement.querySelector('#' + group.getAttribute('aria-labelledby')).textContent.trim()).toBe('Group by:');
    });
  });

  describe('Active filter chips', () => {
    const chipTexts = (): string[] =>
      Array.from(fixture.nativeElement.querySelectorAll('.filter-chip') as NodeListOf<HTMLElement>)
        .map(chip => chip.firstChild!.textContent!.trim());
    const removeChip = (label: string) => {
      (fixture.nativeElement.querySelector(`[aria-label="Remove filter ${label}"]`) as HTMLButtonElement).click();
      fixture.detectChanges();
    };
    const reset = () => {
      (fixture.nativeElement.querySelector('.filter-reset') as HTMLButtonElement).click();
      fixture.detectChanges();
    };

    beforeEach(() => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      component.selectedDistricts = ['Mitte'];
      component.onDistrictFilterChange();
      fixture.detectChanges();
    });

    it('shows the district filter as chips', () => {
      expect(chipTexts()).toEqual(['Mitte']);
      expect(component.chartData.every(point => point.label === 'Mitte')).toBe(true);
    });

    it('removes a district with its chip and updates the chart', () => {
      removeChip('Mitte');

      expect(component.selectedDistricts).toEqual([]);
      expect(component.chartData.some(point => point.label === 'Charlottenburg')).toBe(true);
      expect(chipTexts()).toEqual([]);
    });

    it('resets the districts and the preset', () => {
      reset();

      expect(component.selectedDistricts).toEqual([]);
      expect(presets.service.selectPreset).toHaveBeenCalledWith(null);
    });
  });

  describe('Reloading', () => {
    it('loads the history again on each reload request (refresh button, finished scrape)', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      const initialLoads = flatsService.getHistory.calls.count();

      reload$.next();
      reload$.next();

      expect(flatsService.getHistory.calls.count()).toBe(initialLoads + 2);
    });

    it('stops listening for reload requests when destroyed', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();

      fixture.destroy();

      expect(reload$.observed).toBe(false);
    });
  });

  describe('Empty states', () => {
    const emptyState = (): HTMLElement | null => fixture.nativeElement.querySelector('app-empty-state');
    const heading = (): string | undefined => emptyState()?.querySelector('h2')!.textContent!.trim();
    const clickReset = () => {
      (emptyState()!.querySelector('button') as HTMLButtonElement).click();
      fixture.detectChanges();
    };

    it('offers to start a scrape when there is no history yet', () => {
      flatsService.getHistory.and.returnValue(apiResult({ success: true, data: [], message: 'Success' }));
      fixture.detectChanges();

      expect(heading()).toBe('No history yet');
      expect(emptyState()!.querySelector('app-start-scrape-button')).not.toBeNull();
    });

    it('offers to reset the districts when they have no history, and resets them', () => {
      flatsService.getHistory.and.returnValue(apiResult(mockHistoryResponse));
      fixture.detectChanges();
      component.selectedDistricts = ['Durlach'];
      component.onDistrictFilterChange();
      fixture.detectChanges();

      expect(heading()).toBe('No history data for the selected districts.');
      clickReset();

      expect(component.selectedDistricts).toEqual([]);
      expect(component.chartData.length).toBeGreaterThan(0);
      expect(emptyState()).toBeNull();
    });

    it('shows only the error when loading failed', () => {
      flatsService.getHistory.and.returnValue(throwError(() => new Error('Network error')));
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.alert-danger')).not.toBeNull();
      expect(emptyState()).toBeNull();
    });
  });
});
