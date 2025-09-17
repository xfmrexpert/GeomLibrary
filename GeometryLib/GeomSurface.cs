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
        private static readonly ThreadLocal<Random> _rng = new(() => new Random());

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
            (double minX, double maxX, double minY, double maxY) = Boundary.GetBoundingBox();
            if (minX == maxX) return new GeomPoint(minX, minY);

            var rnd = _rng.Value!;
            for (int attempts = 0; attempts < 10_000; attempts++)
            {
                double x = rnd.NextDouble() * (maxX - minX) + minX;
                double y = rnd.NextDouble() * (maxY - minY) + minY;
                var pt = new GeomPoint(x, y);
                if (!Boundary.IsPointInside(pt)) continue;

                bool inHole = false;
                foreach (var h in Holes)
                {
                    if (h.IsPointInside(pt)) { inHole = true; break; }
                }
                if (!inHole) return pt;
            }
            // Fallback (very unlikely if geometry reasonable)
            return Boundary is { } ? Boundary.Boundary.OfType<GeomLine>().First().pt1 : new GeomPoint(minX, minY);
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
