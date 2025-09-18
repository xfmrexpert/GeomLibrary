// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class Geometry
    {
        public PointCollection Points { get; }
        public List<GeomLine> Lines { get; private set; } = new List<GeomLine>();
        public List<GeomArc> Arcs { get; private set; } = new List<GeomArc>();
        public List<GeomLineLoop> LineLoops { get; private set; } = new List<GeomLineLoop>();
        public List<GeomSurface> Surfaces { get; private set; } = new List<GeomSurface>();
        private readonly Dictionary<(int a, int b), GeomLine> _lineCache = new();
        private readonly Dictionary<(int s, int e, double sweep), GeomArc> _arcCache = new();

        // Incremental bounds
        private bool _hasBounds = false;
        private double _minX, _maxX, _minY, _maxY;

        public double PointTolerance { get; }

        public Geometry(double pointTolerance = 1e-9)
        {
            PointTolerance = pointTolerance;
            Points = new PointCollection(pointTolerance);
        }

        public GeomPoint AddPoint(double x, double y)
        {
            var pt = Points.AddOrGet(x, y, out bool isNew);
            if (isNew) UpdateBoundsWithPoint(pt);
            return pt;
        }

        public GeomPoint AddPoint(double x, double y, double lc)
        {
            var pt = Points.AddOrGet(x, y, lc, out bool isNew);
            if (isNew) UpdateBoundsWithPoint(pt);
            return pt;
        }

        private void UpdateBoundsWithPoint(GeomPoint pt)
        {
            double x = pt.x, y = pt.y;
            if (!_hasBounds)
            {
                _minX = _maxX = x;
                _minY = _maxY = y;
                _hasBounds = true;
            }
            else
            {
                if (x < _minX) _minX = x;
                if (x > _maxX) _maxX = x;
                if (y < _minY) _minY = y;
                if (y > _maxY) _maxY = y;
            }
        }

        public GeomLine AddLine(GeomPoint pt1, GeomPoint pt2)
        {
            int a = Math.Min(pt1.Id, pt2.Id);
            int b = Math.Max(pt1.Id, pt2.Id);
            if (_lineCache.TryGetValue((a, b), out var existing))
                return existing;
            var line = new GeomLine(pt1, pt2);
            _lineCache[(a, b)] = line;
            Lines.Add(line);
            return line;
        }

        public GeomArc AddArc(GeomPoint startPt, GeomPoint endPt, double radius, double sweepAngle)
        {
            var key = (startPt.Id, endPt.Id, sweepAngle);
            if (_arcCache.TryGetValue(key, out var existing))
                return existing;
            var arc = new GeomArc(startPt, endPt, sweepAngle);
            _arcCache[key] = arc;
            Arcs.Add(arc);
            return arc;
        }

        public GeomLineLoop AddLineLoop(params GeomEntity[] entities)
        {
            //TODO: Check for duplicate line loop
            var LineLoop = new GeomLineLoop(entities.ToList<GeomEntity>());
            LineLoops.Add(LineLoop);
            return LineLoop;
        }

        public GeomSurface AddSurface(GeomLineLoop boundary, params GeomLineLoop[] holes)
        {
            //TODO: Check for duplicate surface
            GeomSurface surface = new GeomSurface(boundary, holes);
            Surfaces.Add(surface);
            return surface;
        }

        public GeomLineLoop AddRoundedRectangle(double x_center, double y_center, double h, double w, double corner_radius=0, double lc=0.4)
        {
            if (corner_radius == 0)
            {
                return AddRectangle(x_center, y_center, h, w, lc);
            }

            double ll_x = x_center - w / 2d;
            double ll_y = y_center - h / 2d;
            var LL1 = AddPoint(ll_x + corner_radius, ll_y, lc);
            var LL2 = AddPoint(ll_x, ll_y + corner_radius, lc);
            var UL1 = AddPoint(ll_x, ll_y + h - corner_radius, lc);
            var UL2 = AddPoint(ll_x + corner_radius, ll_y + h, lc);
            var UR1 = AddPoint(ll_x + w - corner_radius, ll_y + h, lc);
            var UR2 = AddPoint(ll_x + w, ll_y + h - corner_radius, lc);
            var LR1 = AddPoint(ll_x + w, ll_y + corner_radius, lc);
            var LR2 = AddPoint(ll_x + w - corner_radius, ll_y, lc);
            // add lines for sides
            var left = AddLine(LL2, UL1);
            var top = AddLine(UL2, UR1);
            var right = AddLine(UR2, LR1);
            var bottom = AddLine(LR2, LL1);
            // add arcs for corners
            var upper_left = AddArc(UL1, UL2, corner_radius, -Math.PI/2d);
            var upper_right = AddArc(UR1, UR2, corner_radius, -Math.PI / 2d);
            var lower_right = AddArc(LR1, LR2, corner_radius, -Math.PI / 2d);
            var lower_left = AddArc(LL1, LL2, corner_radius, -Math.PI / 2d);
            // add boundary loop
            var boundary = AddLineLoop(left, upper_left, top, upper_right, right, lower_right, bottom, lower_left);
            return boundary;
        }

        public GeomLineLoop AddRectangle(double x_center, double y_center, double h, double w, double lc = 0.4)
        {
            double ll_x = x_center - w / 2d;
            double ll_y = y_center - h / 2d;
            var LL = AddPoint(ll_x, ll_y, lc);
            var UL = AddPoint(ll_x, ll_y + h, lc);
            var UR = AddPoint(ll_x + w, ll_y + h, lc);
            var LR = AddPoint(ll_x + w, ll_y, lc);
            // add lines for sides
            var left = AddLine(LL, UL);
            var top = AddLine(UL, UR);
            var right = AddLine(UR, LR);
            var bottom = AddLine(LR, LL);
            // add boundary loop
            var boundary = AddLineLoop(left, top, right, bottom);
            return boundary;
        }

        public BoundingBox GetBounds()
        {
            if (_hasBounds && LineLoops.Count == 0)
            {
                // Fallback to point-derived bounds if no loops yet
                return new BoundingBox(_minX, _minY, _maxX, _maxY);
            }
            // Existing loop-based logic remains (kept for correctness when arcs expand beyond point extents)
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //double maxX = double.MinValue;

            //foreach (var pt in Points)
            //{
            //    if (pt.x > maxX) maxX = pt.x;
            //}

            //double maxY = double.MinValue;
            //foreach (var pt in Points)
            //{
            //    if (pt.y > maxY) maxY = pt.y;
            //}

            foreach (var loop in LineLoops)
            {
                (double loop_minX, double loop_maxX, double loop_minY, double loop_maxY) = loop.GetBoundingBox();
                // Update bounding box
                minX = Math.Min(minX, loop_minX);
                minY = Math.Min(minY, loop_minY);
                maxX = Math.Max(maxX, loop_maxX);
                maxY = Math.Max(maxY, loop_maxY);
            }

            return new BoundingBox(minX, minY, maxX, maxY);
        }

        public GeomSurface? HitTestSurface(double x, double y)
        {
            // Simple linear search; optimize later with spatial index if needed
            foreach (var s in Surfaces)
            {
                if (s.ContainsPoint(x, y))
                    return s;
            }
            return null;
        }
    }
}
