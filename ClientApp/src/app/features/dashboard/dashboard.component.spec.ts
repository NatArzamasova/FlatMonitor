import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardComponent } from './dashboard.component';
import { OverviewTabComponent } from './tabs/overview-tab/overview-tab.component';
import { OffersTabComponent } from './tabs/offers-tab/offers-tab.component';
import { HistoryTabComponent } from './tabs/history-tab/history-tab.component';
import { FlatsService } from '../../core/services/flats.service';
import { of } from 'rxjs';

describe('DashboardComponent (Shell)', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let flatsService: jasmine.SpyObj<FlatsService>;

  beforeEach(async () => {
    const flatsServiceSpy = jasmine.createSpyObj('FlatsService', [
      'getAggregated',
      'getCurrentOffers',
      'getHistory',
      'getScrapeStatus'
    ]);

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: FlatsService, useValue: flatsServiceSpy }
      ]
    }).compileComponents();

    flatsService = TestBed.inject(FlatsService) as jasmine.SpyObj<FlatsService>;
    flatsService.getScrapeStatus.and.returnValue(of({ isScrapingInProgress: false }));
    flatsService.getAggregated.and.returnValue(of({ success: true, data: [], message: 'Success' }));
    flatsService.getCurrentOffers.and.returnValue(of({ success: true, data: [], message: 'Success' }));
    flatsService.getHistory.and.returnValue(of({ success: true, data: [], message: 'Success' }));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create the dashboard shell component', () => {
    expect(component).toBeTruthy();
  });

  it('should have imported the three tab components', () => {
    const compiled = fixture.nativeElement;
    fixture.detectChanges();

    expect(compiled.querySelector('app-overview-tab')).toBeTruthy();
    expect(compiled.querySelector('app-offers-tab')).toBeTruthy();
    expect(compiled.querySelector('app-history-tab')).toBeTruthy();
  });

  it('should render the main tab navigation', () => {
    const compiled = fixture.nativeElement;
    fixture.detectChanges();

    expect(compiled.querySelector('.nav-tabs')).toBeTruthy();
    expect(compiled.querySelector('#dashboard-tab')).toBeTruthy();
    expect(compiled.querySelector('#offers-tab')).toBeTruthy();
    expect(compiled.querySelector('#history-tab')).toBeTruthy();
  });

  it('should render the tab panes with the correct role attributes', () => {
    const compiled = fixture.nativeElement;
    fixture.detectChanges();

    expect(compiled.querySelector('[role="tabpanel"]')).toBeTruthy();
  });
});
