namespace FlatMonitor.Api.Services;

public class ScrapeStateService
{
    private CancellationTokenSource? _currentCancellationTokenSource;
    private readonly ScrapingCounterService _counterService = new();

    public bool IsScrapingInProgress { get; private set; }
    public ScrapingCounterService Counter => _counterService;

    public CancellationToken StartScraping()
    {
        if (IsScrapingInProgress)
            throw new InvalidOperationException("Scraping is already in progress");

        _counterService.Reset();
        _currentCancellationTokenSource = new CancellationTokenSource();
        IsScrapingInProgress = true;
        return _currentCancellationTokenSource.Token;
    }

    public void StopScraping()
    {
        if (_currentCancellationTokenSource != null && !_currentCancellationTokenSource.IsCancellationRequested)
        {
            _currentCancellationTokenSource.Cancel();
        }
    }

    public void FinishScraping()
    {
        IsScrapingInProgress = false;
        _currentCancellationTokenSource?.Dispose();
        _currentCancellationTokenSource = null;
    }
}
