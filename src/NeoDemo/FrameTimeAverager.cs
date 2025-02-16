namespace Veldrid.NeoDemo;

public class FrameTimeAverager(double maxTimeSeconds)
{
    const double DecayRate = .3;

    double _accumulatedTime;
    int _frameCount;

    public double CurrentAverageFrameTimeSeconds { get; private set; }
    public double CurrentAverageFrameTimeMilliseconds => CurrentAverageFrameTimeSeconds * 1000.0;
    public double CurrentAverageFramesPerSecond => 1 / CurrentAverageFrameTimeSeconds;

    public void Reset()
    {
        _accumulatedTime = 0;
        _frameCount = 0;
    }

    public void AddTime(double seconds)
    {
        _accumulatedTime += seconds;
        _frameCount++;
        if (_accumulatedTime >= maxTimeSeconds)
            Average();
    }

    void Average()
    {
        double total = _accumulatedTime;
        double average = total / _frameCount;
        CurrentAverageFrameTimeSeconds =
            CurrentAverageFrameTimeSeconds * DecayRate + average * (1 - DecayRate);

        _accumulatedTime = 0;
        _frameCount = 0;
    }
}
