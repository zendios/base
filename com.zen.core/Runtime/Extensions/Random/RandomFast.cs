using System;
using System.Runtime.CompilerServices;

/// <summary>
/// A high-performance, allocation-free pseudo-random number generator using Xorshift algorithm.
/// Superior to UnityEngine.Random for logic, procedural generation, and threading.
/// </summary>
public struct RandomFast
{
    // The state of the generator.
    private uint _state;

    // Constructor with a specific seed for deterministic results.
    public RandomFast(uint seed)
    {
        // Ensure seed is non-zero, otherwise Xorshift gets stuck.
        _state = seed == 0 ? 0x853c49e6 : seed;
    }

    // Constructor using system time as seed (non-deterministic).
    public RandomFast(bool autoSeed)
    {
        _state = (uint)DateTime.Now.Ticks;
        if (_state == 0) _state = 0x853c49e6;
    }

    /// <summary>
    /// Returns a random integer between [min, max).
    /// </summary>
    /// <param name="min">Inclusive lower bound.</param>
    /// <param name="max">Exclusive upper bound.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextInt(int min, int max)
    {
        if (min >= max) return min;

        // Calculate the range using uint to prevent overflow issues with large negative numbers
        uint range = (uint)(max - min);

        // Get a random uint and map it to the range using modulo
        // Note: Modulo introduces slight bias, but for game logic, it's usually negligible and faster than floating point conversion.
        return min + (int)(NextUInt() % range);
    }

    /// <summary>
    /// Returns a random integer in range [0, int.MaxValue).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextInt()
    {
        // Mask the sign bit to ensure positive integer
        return (int)(NextUInt() & 0x7FFFFFFF);
    }

    /// <summary>
    /// Returns a random float between 0.0f (inclusive) and 1.0f (inclusive).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NextFloat()
    {
        // Convert random uint to float [0, 1] directly via bit manipulation (faster than division)
        // 0x7FFFFF is the mantissa mask for float.
        return (NextUInt() & 0x7FFFFF) * (1.0f / 8388607.0f);
    }

    /// <summary>
    /// Returns a random boolean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NextBool()
    {
        return (NextUInt() & 1) == 0;
    }

    /// <summary>
    /// Core Xorshift32 algorithm. Very fast bitwise operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }
}