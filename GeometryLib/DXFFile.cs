// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace GeometryLib
{
    public class DxfFile
    {
        private DxfDocument doc = new DxfDocument();
        private Line CreateLine(GeomLine line, Layer layer = null)
        {
            Line dxfLine = new Line(new Vector2(line.pt1.x, line.pt1.y), new Vector2(line.pt2.x, line.pt2.y));
            if (layer != null)
            {
                dxfLine.Layer = layer;
            }
            doc.Entities.Add(dxfLine);
            return dxfLine;
        }

        private Arc CreateArc(GeomArc arc, Layer layer = null)
        {
            var startPt = new Vector2(arc.StartPt.x, arc.StartPt.y);
            var endPt = new Vector2(arc.EndPt.x, arc.EndPt.y);
            var radius = arc.Radius;

            int eps = -1;
            //Need to calculate the center point from start, end, radius, sweep angle
            Vector2 vecStartToEnd = new Vector2(endPt.X - startPt.X, endPt.Y - startPt.Y);
            double d = vecStartToEnd.Modulus();
            Vector2 m = new Vector2((startPt.X + endPt.X) / 2, (startPt.Y + endPt.Y) / 2);
            Vector2 n_star = new Vector2(-(endPt.Y - startPt.Y) / d, (endPt.X - startPt.X) / d);
            double h = Math.Sqrt(radius * radius - d * d / 4);
            var c = m + eps * h * n_star;
            var startAngle = Vector2.Angle(c, startPt) * 180 / Math.PI;
            var endAngle = Vector2.Angle(c, endPt) * 180 / Math.PI;
            Arc new_arc;
            if (h != 0)
            {
                new_arc = new Arc(c, radius, endAngle, startAngle);
            }
            else
            {
                new_arc = new Arc(c, radius, endAngle, startAngle);
            }
            if (layer != null)
            {
                new_arc.Layer = layer;
            }
            doc.Entities.Add(new_arc);
            return new_arc;
        }
        
        public void CreateFromGeometry(Geometry geometry)
        {
            int i = 0;
            foreach (var surface in geometry.Surfaces)
            {
                var layer = new netDxf.Tables.Layer($"Surface_{i}");
                i++;
                doc.Layers.Add(layer);
                
                foreach (var entity in surface.Boundary.Boundary)
                {
                    if (entity is GeomLine line)
                    {
                        CreateLine(line, layer);
                    }
                    else if (entity is GeomArc arc)
                    {
                        CreateArc(arc, layer);
                    }
                }
                foreach (var hole in surface.Holes)
                {
                    foreach (var entity in hole.Boundary)
                    {
                        if (entity is GeomLine line)
                        {
                            CreateLine(line, layer);
                        }
                        else if (entity is GeomArc arc)
                        {
                            CreateArc(arc, layer);
                        }
                    }
                }
            }

            doc.Save("geom.dxf");
        }
    }
}
