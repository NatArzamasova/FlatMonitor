namespace workInProgress.Api.Services;

public class ScrapeStateService
{
    private CancellationTokenSource? _currentCancellationTokenSource;
    public bool IsScrapingInProgress { get; private set; }

    public CancellationToken StartScraping()
    {
        if (IsScrapingInProgress)
            throw new InvalidOperationException("Scraping is already in progress");

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
