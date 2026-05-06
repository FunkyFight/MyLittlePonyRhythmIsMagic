using System;

public readonly struct BeatTick : IComparable<BeatTick>, IEquatable<BeatTick>
{
    public const long TicksPerBeat = 960;

    public BeatTick(long ticks)
    {
        Ticks = ticks;
    }

    public long Ticks { get; }

    public static BeatTick FromBeat(double beat)
    {
        if (double.IsNaN(beat) || double.IsInfinity(beat))
            return new BeatTick(0);

        return new BeatTick((long)Math.Round(beat * TicksPerBeat, MidpointRounding.AwayFromZero));
    }

    public double ToBeat()
    {
        return Ticks / (double)TicksPerBeat;
    }

    public int CompareTo(BeatTick other)
    {
        return Ticks.CompareTo(other.Ticks);
    }

    public bool Equals(BeatTick other)
    {
        return Ticks == other.Ticks;
    }

    public override bool Equals(object obj)
    {
        return obj is BeatTick other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Ticks.GetHashCode();
    }

    public static bool operator ==(BeatTick left, BeatTick right) => left.Equals(right);
    public static bool operator !=(BeatTick left, BeatTick right) => !left.Equals(right);
    public static bool operator <(BeatTick left, BeatTick right) => left.Ticks < right.Ticks;
    public static bool operator <=(BeatTick left, BeatTick right) => left.Ticks <= right.Ticks;
    public static bool operator >(BeatTick left, BeatTick right) => left.Ticks > right.Ticks;
    public static bool operator >=(BeatTick left, BeatTick right) => left.Ticks >= right.Ticks;
}
