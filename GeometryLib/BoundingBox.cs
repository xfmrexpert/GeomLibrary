// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

namespace GeometryLib
{
    // BoundingBox class for encapsulating bounds
    public class BoundingBox
    {
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        public double Width {  get => MaxX - MinX; }
        public double Height { get => MaxY - MinY; }

        public GeomPoint Center { get => new GeomPoint((MinX + MaxX) / 2, (MinY + MaxY) / 2); }

        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public override string ToString()
        {
            return $"BoundingBox: MinX={MinX}, MinY={MinY}, MaxX={MaxX}, MaxY={MaxY}";
        }
    }
}
