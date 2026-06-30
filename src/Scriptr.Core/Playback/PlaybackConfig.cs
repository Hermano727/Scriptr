namespace Scriptr.Core.Playback;

public readonly struct PlaybackConfig
{
    public float    SpeedMultiplier { get; }
    public LoopMode Mode            { get; }
    public int      RepeatCount     { get; }

    private PlaybackConfig(float speed, LoopMode mode, int count)
    {
        SpeedMultiplier = speed > 0f ? speed : 1f;
        Mode            = mode;
        RepeatCount     = count;
    }

    public static PlaybackConfig Once(float speed = 1.0f)            => new(speed, LoopMode.PlayOnce,   1);
    public static PlaybackConfig Repeat(int n, float speed = 1.0f)   => new(speed, LoopMode.RepeatN,    n);
    public static PlaybackConfig Forever(float speed = 1.0f)         => new(speed, LoopMode.Continuous, 0);
}
