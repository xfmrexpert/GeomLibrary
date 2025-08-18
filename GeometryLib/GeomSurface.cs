// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class GeomSurface : GeomEntity
    {
        public override GeomEntityType Type
        {
            get
            {
                return GeomEntityType.Surface;
            }
        }

        public GeomLineLoop Boundary { get; set; }

        public List<GeomLineLoop> Holes { get; set; } = new List<GeomLineLoop>();

        public GeomSurface(GeomLineLoop boundary, params GeomLineLoop[] holes)
        {
            Boundary = boundary;
            Holes = holes.ToList();
        }

        public GeomPoint GetRandomPointInSurface()
        {
            Random random = new Random();
            (double minX, double maxX, double minY, double maxY) = Boundary.GetBoundingBox();
            if (minX == maxX) return new GeomPoint(0.01,0);
            while (true)
            {
                // Generate random point within boundary box
                double x = random.NextDouble() * (maxX - minX) + minX;
                double y = random.NextDouble() * (maxY - minY) + minY;
                GeomPoint randomPoint = new GeomPoint(x, y);

                // Check if the point is inside the boundary polygon
                if (Boundary.IsPointInside(randomPoint))
                {
                    // Check if the point is inside any of the holes
                    bool insideHole = false;
                    foreach (var hole in Holes)
                    {
                        if (hole.IsPointInside(randomPoint))
                        {
                            insideHole = true;
                            break;
                        }
                    }

                    if (!insideHole)
                    {
                        return randomPoint;  // The point is inside the polygon and not inside any hole
                    }
                }
            }
        }

        public bool ContainsPoint(double x, double y)
        {
            var test = new GeomPoint(x, y);
            if (!Boundary.IsPointInside(test))
                return false;
            foreach (var hole in Holes)
            {
                if (hole.IsPointInside(test))
                    return false;
            }
            return true;
        }
    }
}
