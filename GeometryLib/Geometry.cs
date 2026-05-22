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
        // Dedup cache for circle arcs constructed by 3 points (start, center, end).
        // Two callers that build geometrically identical arcs (e.g. an angle ring's
        // inside fillet sitting on top of a winding's corner fillet) must reuse the
        // SAME GeomArc instance — otherwise GmshFile emits two `Circle (...)` entities
        // for the same physical curve, gmsh tessellates them independently, and the
        // resulting duplicate-coordinate interior nodes get merged by MFEM's Finalize()
        // and end up tying together boundary edges that belong to different physical
        // attributes (the "Conflicting Boundary Conditions" failure mode).
        private readonly Dictionary<(int s, int c, int e), GeomArc> _circleArcCache = new();

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
            {
                // Safety net: if a previous removal left a stale cache entry whose line is
                // no longer in `Lines`, treat it as a miss and rebuild instead of handing
                // back a dead instance.
                if (Lines.Contains(existing))
                    return existing;
                _lineCache.Remove((a, b));
            }
            var line = new GeomLine(pt1, pt2);
            _lineCache[(a, b)] = line;
            Lines.Add(line);
            return line;
        }

        public GeomArc AddArc(GeomPoint startPt, GeomPoint endPt, double radius, double sweepAngle)
        {
            var key = (startPt.Id, endPt.Id, sweepAngle);
            if (_arcCache.TryGetValue(key, out var existing))
            {
                // Same safety net as AddLine: ignore stale cache entries that no longer
                // correspond to a live arc in `Arcs`.
                if (Arcs.Contains(existing))
                    return existing;
                _arcCache.Remove(key);
            }
            var arc = new GeomArc(startPt, endPt, sweepAngle);
            _arcCache[key] = arc;
            Arcs.Add(arc);
            return arc;
        }

        /// <summary>
        /// Remove a line from the geometry and evict it from the deduplication cache so
        /// future <see cref="AddLine(GeomPoint, GeomPoint)"/> calls with the same endpoints
        /// allocate a fresh instance.
        /// </summary>
        public bool RemoveLine(GeomLine line)
        {
            if (line == null) return false;
            int a = Math.Min(line.pt1.Id, line.pt2.Id);
            int b = Math.Max(line.pt1.Id, line.pt2.Id);
            if (_lineCache.TryGetValue((a, b), out var cached) && ReferenceEquals(cached, line))
                _lineCache.Remove((a, b));
            return Lines.Remove(line);
        }

        /// <summary>
        /// Remove an arc from the geometry and evict it from the deduplication cache. The
        /// cache key includes the sweep angle, so all entries pointing at this instance are
        /// removed.
        /// </summary>
        public bool RemoveArc(GeomArc arc)
        {
            if (arc == null) return false;
            // The cache key includes the sweep angle, but we don't want to depend on the
            // exact stored key. Sweep is small in count, so just scrub any entry that maps
            // to this instance.
            var staleKeys = new List<(int s, int e, double sweep)>();
            foreach (var kvp in _arcCache)
            {
                if (ReferenceEquals(kvp.Value, arc)) staleKeys.Add(kvp.Key);
            }
            foreach (var k in staleKeys) _arcCache.Remove(k);
            // Likewise drop any entry in the circle-arc (3-point) dedup cache so a later
            // AddCircleArc with the same point triple allocates a fresh instance.
            var staleCircleKeys = new List<(int s, int c, int e)>();
            foreach (var kvp in _circleArcCache)
            {
                if (ReferenceEquals(kvp.Value, arc)) staleCircleKeys.Add(kvp.Key);
            }
            foreach (var k in staleCircleKeys) _circleArcCache.Remove(k);
            return Arcs.Remove(arc);
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

        public GeomArc AddCircleArc(GeomPoint startPt, GeomPoint center, GeomPoint endPt)
        {
            // Dedup on the ordered triple of point IDs. We deliberately do NOT collapse
            // (start, center, end) with its reverse (end, center, start) because the two
            // describe different arc directions and may legitimately appear in different
            // loops with opposite orientation.
            var key = (startPt.Id, center.Id, endPt.Id);
            if (_circleArcCache.TryGetValue(key, out var existing) && Arcs.Contains(existing))
                return existing;

            GeomArc arc = new GeomArc(startPt, center, endPt);
            Arcs.Add(arc);
            _circleArcCache[key] = arc;
            return arc;
        }

        /// <summary>
        /// Create a rectangle surface and apply independent fillets at each corner.
        /// (r0, z0) is lower-left.
        /// Returns surface tag.
        /// </summary>
        public GeomSurface AddRectWithCornerRadii(
            double r0, double z0, double w, double h,
            double rTL, double rTR, double rBR, double rBL)
        {
            double startX = r0 + rBL;
            double startY = z0;

            GeomPoint pFirst = this.AddPoint(startX, startY);

            var loopCurves = new List<GeomEntity>();

            // --- 1. Bottom Edge: BL Exit -> BR Entry ---
            double brEntryX = r0 + w - rBR;
            double brEntryY = z0;

            GeomPoint currentPt = null;

            if (Math.Abs(brEntryX - startX) > 1e-6)
            {
                GeomPoint pNext = this.AddPoint(brEntryX, brEntryY);
                loopCurves.Add(this.AddLine(pFirst, pNext));
                currentPt = pNext;
            }

            // --- 2. BR Corner: BR Entry -> BR Exit ---
            if (rBR > 0)
            {
                double brExitX = r0 + w;
                double brExitY = z0 + rBR;
                GeomPoint pExit = this.AddPoint(brExitX, brExitY);
                GeomPoint center = this.AddPoint(r0 + w - rBR, z0 + rBR, 0);
                loopCurves.Add(this.AddCircleArc(currentPt, center, pExit));
                currentPt = pExit;
            }

            // --- 3. Right Edge: BR Exit -> TR Entry ---
            double trEntryX = r0 + w;
            double trEntryY = z0 + h - rTR;

            if (Math.Abs(trEntryY - (z0 + rBR)) > 1e-6)
            {
                GeomPoint pNext = this.AddPoint(trEntryX, trEntryY);
                loopCurves.Add(this.AddLine(currentPt, pNext));
                currentPt = pNext;
            }

            // --- 4. TR Corner: TR Entry -> TR Exit ---
            if (rTR > 0)
            {
                double trExitX = r0 + w - rTR;
                double trExitY = z0 + h;
                GeomPoint pExit = this.AddPoint(trExitX, trExitY);
                GeomPoint center = this.AddPoint(r0 + w - rTR, z0 + h - rTR);
                loopCurves.Add(this.AddCircleArc(currentPt, center, pExit));
                currentPt = pExit;
            }

            // --- 5. Top Edge: TR Exit -> TL Entry ---
            double tlEntryX = r0 + rTL;
            double tlEntryY = z0 + h;

            if (Math.Abs(tlEntryX - (r0 + w - rTR)) > 1e-6)
            {
                GeomPoint pNext = this.AddPoint(tlEntryX, tlEntryY);
                loopCurves.Add(this.AddLine(currentPt, pNext));
                currentPt = pNext;
            }

            // --- 6. TL Corner: TL Entry -> TL Exit ---
            if (rTL > 0)
            {
                double tlExitX = r0;
                double tlExitY = z0 + h - rTL;
                GeomPoint pExit = this.AddPoint(tlExitX, tlExitY);
                GeomPoint center = this.AddPoint(r0 + rTL, z0 + h - rTL);
                loopCurves.Add(this.AddCircleArc(currentPt, center, pExit));
                currentPt = pExit;
            }

            // --- 7. Left Edge: TL Exit -> BL Entry ---
            double blEntryX = r0;
            double blEntryY = z0 + rBL;

            if (Math.Abs(blEntryY - (z0 + h - rTL)) > 1e-6)
            {
                if (rBL == 0)
                {
                    loopCurves.Add(this.AddLine(currentPt, pFirst));
                    currentPt = pFirst;
                }
                else
                {
                    GeomPoint pNext = this.AddPoint(blEntryX, blEntryY);
                    loopCurves.Add(this.AddLine(currentPt, pNext));
                    currentPt = pNext;
                }
            }

            // --- 8. BL Corner: BL Entry -> BL Exit (Start) ---
            if (rBL > 0)
            {
                GeomPoint center = this.AddPoint(r0 + rBL, z0 + rBL);
                loopCurves.Add(this.AddCircleArc(currentPt, center, pFirst));
                currentPt = pFirst;
            }

            // Create loop and surface
            GeomLineLoop loop = this.AddLineLoop(loopCurves.ToArray());
            GeomSurface s = this.AddSurface(loop);

            return s;
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
