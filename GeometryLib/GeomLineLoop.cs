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

        // Ray-casting algorithm to check if the point is inside the polygon
        public bool IsPointInside(GeomPoint point)
        {
            bool result = false;
            foreach (var segment in Boundary)
            {
                if (segment.Contains(point))
                    return true;  // Directly on a segment is considered "inside"
            }

            int j = Boundary.Count - 1;
            for (int i = 0; i < Boundary.Count; i++)
            {
                GeomLine line = Boundary[i] as GeomLine;
                if (line != null)
                {
                    if ((line.pt1.y < point.y && line.pt2.y >= point.y || line.pt2.y < point.y && line.pt1.y >= point.y)
                        && (line.pt1.x + (point.y - line.pt1.y) / (line.pt2.y - line.pt1.y) * (line.pt2.x - line.pt1.x) < point.x))
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public bool IsPointInsideOld(GeomPoint pt)
        {
            bool result = false;

            List<GeomPoint> vertices = new List<GeomPoint>();

            foreach (GeomEntity e in Boundary)
            {
                if (e is GeomLine line)
                {
                    vertices.Add(line.pt2);
                }
                else if (e is GeomArc arc)
                {
                    vertices.Add(arc.EndPt);
                }
            }

            int j = vertices.Count - 1;

            for (int i = 0; i < vertices.Count; i++)
            {
                if ((vertices[i].y < pt.y && vertices[j].y >= pt.y || vertices[j].y < pt.y && vertices[i].y >= pt.y)
                    && (vertices[i].x + (pt.y - vertices[i].y) / (vertices[j].y - vertices[i].y) * (vertices[j].x - vertices[i].x) < pt.x))
                {
                    result = !result;
                }
                j = i;
            }
            return result;
        }
    }
}
