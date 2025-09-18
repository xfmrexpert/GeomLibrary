using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryLib
{
    public class PointCollection : IReadOnlyList<GeomPoint>
    {
        private readonly List<GeomPoint> _points = new();
        // Hash bucket -> list (handle collisions within tolerance)
        private readonly Dictionary<(long, long), List<GeomPoint>> _buckets = new();

        public double Tolerance { get; }
        private readonly double _invTol;

        public PointCollection(double tolerance = 0.001)
        {
            if (tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            Tolerance = tolerance;
            _invTol = 1.0 / tolerance;
        }

        private (long, long) Key(double x, double y)
            => (unchecked((long)Math.Round(x * _invTol)),
                unchecked((long)Math.Round(y * _invTol)));

        public GeomPoint AddOrGet(double x, double y, double? lc, out bool isNew)
        {
            var key = Key(x, y);
            if (_buckets.TryGetValue(key, out var list))
            {
                foreach (var p in list)
                {
                    // Fast axis-aligned tolerance check
                    if (Math.Abs(p.x - x) <= Tolerance && Math.Abs(p.y - y) <= Tolerance)
                    {
                        if (lc.HasValue) p.lc = lc.Value;
                        isNew = false;
                        return p;
                    }
                }
            }
            var pt = new GeomPoint(x, y);
            if (lc.HasValue) pt.lc = lc.Value;
            _points.Add(pt);
            (list ??= (_buckets[key] = new List<GeomPoint>())).Add(pt);
            isNew = true;
            return pt;
        }

        public GeomPoint AddOrGet(double x, double y, out bool isNew)
            => AddOrGet(x, y, null, out isNew);

        public int Count => _points.Count;
        public GeomPoint this[int index] => _points[index];
        public IEnumerator<GeomPoint> GetEnumerator() => _points.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}