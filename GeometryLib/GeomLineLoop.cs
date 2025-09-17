// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class GeomLineLoop : GeomEntity
    {
        // Precomputed vertices (endpoints in order) for fast inclusion tests
        private GeomPoint[] _polyVertices = Array.Empty<GeomPoint>();

        public override GeomEntityType Type
        {
            get
            {
                return GeomEntityType.LineLoop;
            }
        }

        public List<GeomEntity> Boundary { get; set; } = new List<GeomEntity>();

        public GeomLineLoop(List<GeomEntity> boundary)
        {
            Boundary = boundary;
            RebuildVertexCache();
        }

        private void RebuildVertexCache()
        {
            // Build polygon vertices in traversal order
            var verts = new List<GeomPoint>(Boundary.Count);
            foreach (var e in Boundary)
            {
                if (e is GeomLine l) verts.Add(l.pt2);
                else if (e is GeomArc a) verts.Add(a.EndPt);
            }
            _polyVertices = verts.ToArray();
        }

        public (double minX, double maxX, double minY, double MaxY) GetBoundingBox()
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (GeomEntity segment in Boundary)
            {
                switch (segment)
                {
                    case GeomLine line:
                        minX = Math.Min(minX, Math.Min(line.pt1.x, line.pt2.x));
                        minY = Math.Min(minY, Math.Min(line.pt1.y, line.pt2.y));
                        maxX = Math.Max(maxX, Math.Max(line.pt1.x, line.pt2.x));
                        maxY = Math.Max(maxY, Math.Max(line.pt1.y, line.pt2.y));
                        break;

                    case GeomArc arc:
                        minX = Math.Min(minX, Math.Min(arc.StartPt.x, arc.EndPt.x));
                        minY = Math.Min(minY, Math.Min(arc.StartPt.y, arc.EndPt.y));
                        maxX = Math.Max(maxX, Math.Max(arc.StartPt.x, arc.EndPt.x));
                        maxY = Math.Max(maxY, Math.Max(arc.StartPt.y, arc.EndPt.y));

                        // Check extrema along the cardinal directions (0, π/2, π, 3π/2)
                        double[] cardinalAngles = { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };

                        foreach (double angle in cardinalAngles)
                        {
                            // Check if the cardinal direction angle lies within the arc's angular range
                            if (arc.IsAngleInArc(angle))
                            {
                                // Compute the extrema points
                                double x = arc.Center.x + arc.Radius * Math.Cos(angle);
                                double y = arc.Center.y + arc.Radius * Math.Sin(angle);

                                // Update bounding box
                                minX = Math.Min(minX, x);
                                minY = Math.Min(minY, y);
                                maxX = Math.Max(maxX, x);
                                maxY = Math.Max(maxY, y);
                            }
                        }
                        break;
                }
            }
            return (minX, maxX, minY, maxY);
        }

        // Ray-casting using cached vertex array; ignores arcs curvature (ok for containment heuristic)
        public bool IsPointInside(GeomPoint point)
        {
            foreach (var segment in Boundary)
                if (segment.Contains(point)) return true;

            bool inside = false;
            var verts = _polyVertices;
            int j = verts.Length - 1;
            for (int i = 0; i < verts.Length; i++)
            {
                var vi = verts[i];
                var vj = verts[j];
                bool intersect = ((vi.y > point.y) != (vj.y > point.y)) &&
                                 (point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y + 1e-16) + vi.x);
                if (intersect) inside = !inside;
                j = i;
            }
            return inside;
        }
    }
}
