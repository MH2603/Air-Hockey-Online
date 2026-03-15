using System;
using System.Runtime.CompilerServices;

namespace MH.Core
{
    /// <summary>
    /// A 2D vector with float components.
    /// </summary>
    public struct CustomVector2 : IEquatable<CustomVector2>
    {
        public float x;
        public float y;

        public static readonly CustomVector2 Zero = new CustomVector2(0f, 0f);
        public static readonly CustomVector2 One = new CustomVector2(1f, 1f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CustomVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator +(CustomVector2 a, CustomVector2 b)
        {
            return new CustomVector2(a.x + b.x, a.y + b.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator -(CustomVector2 a, CustomVector2 b)
        {
            return new CustomVector2(a.x - b.x, a.y - b.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator *(CustomVector2 a, int scale)
        {
            return new CustomVector2(a.x * scale, a.y * scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator *(int scale, CustomVector2 a) => a * scale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator *(CustomVector2 a, float scale)
        {
            return new CustomVector2(a.x * scale, a.y * scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator *(float scale, CustomVector2 a) => a * scale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator /(CustomVector2 a, int divisor)
        {
            if (divisor == 0) throw new DivideByZeroException();
            return new CustomVector2(a.x / divisor, a.y / divisor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator /(CustomVector2 a, float divisor)
        {
            if (Math.Abs(divisor) < 1e-9f) throw new DivideByZeroException();
            return new CustomVector2(a.x / divisor, a.y / divisor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CustomVector2 operator -(CustomVector2 a)
        {
            return new CustomVector2(-a.x, -a.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CustomVector2 a, CustomVector2 b) => a.x == b.x && a.y == b.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CustomVector2 a, CustomVector2 b) => a.x != b.x || a.y != b.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(CustomVector2 other) => x == other.x && y == other.y;

        public override readonly bool Equals(object obj) => obj is CustomVector2 other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(CustomVector2 a, CustomVector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public override readonly string ToString() => $"({x}, {y})";
    }
}
