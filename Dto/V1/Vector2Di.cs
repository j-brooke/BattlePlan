using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BattlePlan.Dto.V1
{
    public struct Vector2Di
    {
        public short X { get; }
        public short Y { get; }

        public Vector2Di(short x, short y)
        {
            this.X = x;
            this.Y = y;
        }

        // TODO: create a converter class for reading/writing, instead of cluttering up this
        // struct with references to libraries.
        [JsonConstructor]
        public Vector2Di(double x, double y)
        {
            this.X = (short)Math.Round(x);
            this.Y = (short)Math.Round(y);
        }
        public double Magnitude()
        {
            return Math.Sqrt(this.X * this.X + this.Y * this.Y);
        }
    }
}
