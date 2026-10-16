namespace workInProgress.Api.Services;

public class ScrapingCounterService
{
    private int _count = 0;

    public int Count => _count;

    public void IncrementBy(int count)
    {
        _count += count;
    }

    public void Reset()
    {
        _count = 0;
    }
}
