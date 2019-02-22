using System;
using System.Collections.Generic;

namespace BattlePlan.Model
{
    public struct Vector2Di : IEquatable<Vector2Di>
    {
        public short X { get; }
        public short Y { get; }

        public Vector2Di(short x, short y)
        {
            this.X = x;
            this.Y = y;
        }

        public Vector2Di(double x, double y)
        {
            this.X = (short)Math.Round(x);
            this.Y = (short)Math.Round(y);
        }
        public double Magnitude()
        {
            return Math.Sqrt(this.X * this.X + this.Y * this.Y);
        }

        public double DistanceTo(Vector2Di other)
        {
            return (this-other).Magnitude();
        }

        public bool Equals(Vector2Di other)
        {
           return this==other;
        }

        public override bool Equals(object other)
        {
            if (other == null || GetType() != other.GetType())
                return false;

            var otherAsVec = (Vector2Di)other;
            return this==otherAsVec;
        }

        public override int GetHashCode()
        {
            return this.Y.GetHashCode() * _hashMultiplier + this.X.GetHashCode();
        }

        public override string ToString()
        {
            return $"({this.X},{this.Y})";
        }

        private const int _hashMultiplier = 7187;

        public static Vector2Di operator+(Vector2Di a, Vector2Di b)
        {
            return new Vector2Di((short)(a.X + b.X), (short)(a.Y + b.Y));
        }
        public static Vector2Di operator-(Vector2Di a, Vector2Di b)
        {
            return new Vector2Di((short)(a.X - b.X), (short)(a.Y - b.Y));
        }

        public static bool operator==(Vector2Di a, Vector2Di b)
        {
            return a.X==b.X & a.Y==b.Y;
        }
        public static bool operator!=(Vector2Di a, Vector2Di b)
        {
            return !(a==b);
        }
    }
}
