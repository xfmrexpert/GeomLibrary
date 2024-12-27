// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace TDAP
{
    public class GmshFile
    {
        public int ElementOrder {get; set;} = 1;
        public string Filename {get; set;}
        public List<GmshPoint> points = new List<GmshPoint>();
        public List<GmshLine> lines = new List<GmshLine>();
        public List<GmshArc> arcs = new List<GmshArc>();
        public List<GmshCurveLoop> curve_loops = new List<GmshCurveLoop>();
        public List<GmshPlaneSurface> plane_surfaces = new List<GmshPlaneSurface>();
        public List<GmshPhysicalCurve> physical_curves = new List<GmshPhysicalCurve>();
        public List<GmshPhysicalSurface> physical_surfaces = new List<GmshPhysicalSurface>();

        public double lc { get; set; } = 0.1;

        private int nextGeoID = 1;
        private int nextPhysID = 1;

        public GmshFile(string filename)
        {
            Filename = filename;
        }

        public GmshPoint? FindPoint(double x, double y, double z)
        {
            return points.Find(p => p.x.AboutEquals(x) && p.y.AboutEquals(y) && p.z.AboutEquals(z));
        }

        public GmshPoint? FindPoint(GmshPoint pt)
        {
            return FindPoint(pt.x, pt.y, pt.z);
        }

        public GmshPoint? FindPoint(GeomPoint pt)
        {
            return FindPoint(pt.x, pt.y, 0);
        }

        public GmshLine? FindLine(double x1, double y1, double x2, double y2)
        {
            var pt1 = FindPoint(x1, y1, 0);
            var pt2 = FindPoint(x2, y2, 0);
            if (pt1 != null && pt2 != null)
            {
                return lines.Find(l => (l.StartPt == pt1 && l.EndPt == pt2) || (l.StartPt == pt2 && l.EndPt == pt1));
            }
            else
            {
                return null;
            }
        }

        public GmshLine? FindLine(GmshPoint pt1, GmshPoint pt2)
        {
            return FindLine(pt1.x, pt1.y, pt2.x, pt2.y);
        }

        public GmshLine? FindLine(GeomPoint pt1, GeomPoint pt2)
        {
            return FindLine(pt1.x, pt1.y, pt2.x, pt2.y);
        }

        public GmshLine? FindLine(GeomLine line)
        {
            return FindLine(line.pt1, line.pt2);
        }

        public GmshArc? FindArc(GmshPoint startPt, GmshPoint centerPt, GmshPoint endPt)
        {
            return arcs.Find(a => a.StartPt == startPt && a.CenterPt == centerPt && a.EndPt == endPt);
        }

        public GmshArc? FindArc(GeomArc arc)
        {
            var startPt = FindPoint(arc.StartPt);
            var endPt = FindPoint(arc.EndPt);
            var centerPt = FindPoint(arc.Center);
            if (startPt == null || endPt == null || centerPt == null) return null;
            if (arc.SweepAngle >= 0)
            {
                return FindArc(startPt, centerPt, endPt);
            }
            else
            {
                return FindArc(endPt, centerPt, startPt);
            }
        }

        public GmshCurveLoop? FindCurveLoop(GeomLineLoop in_loop)
        {
            foreach (var loop in curve_loops)
            {
                if (loop.IsMatchingLoop(in_loop)) return loop;
            }
            Debugger.Break();
            return null;
        }

        public GmshPoint CreateNewPoint(double x, double y, double z, int ID = -9999, double lc = 0.4)
        {
            var pt = FindPoint(x, y, z);
            if (pt == null)
            {
                pt = new GmshPoint(x, y, z, -9999, lc);
                if (ID == -9999)
                {
                    pt.ID = nextGeoID;
                    nextGeoID++;
                }
                else
                {
                    pt.ID = ID;
                }
                points.Add(pt);
            }
            else
            {
                if (ID != -9999 && ID != pt.ID) throw new Exception($"Trying to reassign existing GeomPoint ID {pt.ID} while adding matching point with different ID {ID}");
            }

            return pt;
            
        }

        public GmshLine CreateNewLine(GmshPoint pt1, GmshPoint pt2, int ID = -9999)
        {
            var line = FindLine(pt1, pt2);
            if (line == null)
            {
                line = new GmshLine(pt1, pt2);
                if (ID == -9999)
                {
                    line.ID = nextGeoID;
                    nextGeoID++;
                }
                else
                {
                    line.ID = ID;
                }
                lines.Add(line);
            }
            else
            {
                if (ID != -9999 && ID != line.ID) throw new Exception($"Trying to reassign existing GeomLine ID {line.ID} while adding matching line with different ID {ID}");
            }

            return line;
        }

        public GmshArc CreateNewArc(GmshPoint startPt, GmshPoint centerPt, GmshPoint endPt, int ID = -9999)
        {
            var arc = new GmshArc(startPt, centerPt, endPt);
            if (ID == -9999)
            {
                arc.ID = nextGeoID;
                nextGeoID++;
            }
            else
            {
                arc.ID = ID;
            }
            arcs.Add(arc);

            return arc;
        }

        public GmshCurveLoop CreateNewCurveLoop(List<GmshCurvilinearEntity> segments, int ID=-9999)
        {
            var loop = new GmshCurveLoop(segments);
            if (ID == -9999)
            {
                loop.ID = nextGeoID;
                nextGeoID++;
            }
            else
            {
                loop.ID = ID;
            }
            curve_loops.Add(loop);

            return loop;
        }

        public GmshPlaneSurface CreateNewSurface(GmshCurveLoop boundary, List<GmshCurveLoop> holes, int ID=-9999)
        {
            var surface = new GmshPlaneSurface(boundary, holes);
            if (ID == -9999)
            {
                surface.ID = nextGeoID;
                nextGeoID++;
            }
            else
            {
                surface.ID = ID;
            }
            plane_surfaces.Add(surface);

            return surface;
        }

        public void gmshRectangle(double ll_x, double ll_y, double ur_x, double ur_y)
        {
            GmshPoint ll = new GmshPoint(ll_x, ll_y, 0);
            GmshPoint lr = new GmshPoint(ur_x, ll_y, 0);
            GmshPoint ur = new GmshPoint(ur_x, ur_y, 0);
            GmshPoint ul = new GmshPoint(ll_x, ur_y, 0);
            points.Add(ll);
            points.Add(lr);
            points.Add(ur);
            points.Add(ul);
            GmshLine bottom = new GmshLine(ll, lr);
            GmshLine right = new GmshLine(lr, ur);
            GmshLine top = new GmshLine(ur, ul);
            GmshLine left = new GmshLine(ul, ll);
            lines.Add(bottom);
            lines.Add(right);
            lines.Add(top);
            lines.Add(left);
            List<GmshCurvilinearEntity> rect_lines = new List<GmshCurvilinearEntity>();
            rect_lines.Add(bottom);
            rect_lines.Add(right);
            rect_lines.Add(top);
            rect_lines.Add(left);
            GmshCurveLoop rect = new GmshCurveLoop(rect_lines);
            curve_loops.Add(rect);
            //TODO: Create plane and physical surfaces?  Maybe with a flag?
        }

        public void CreateFromGeometry(Geometry geometry)
        {
            points.Clear();
            lines.Clear();
            arcs.Clear();
            curve_loops.Clear();
            plane_surfaces.Clear();
            physical_curves.Clear();
            physical_surfaces.Clear();

            nextGeoID = 1;
            nextPhysID = 1;

            foreach(var point in geometry.Points)
            {
                CreateNewPoint(point.x, point.y, 0, -9999, point.lc);
            }

            foreach(var line in geometry.Lines)
            {
                var pt1 = FindPoint(line.pt1);
                var pt2 = FindPoint(line.pt2);
                if (pt1 != null && pt2 != null)
                {
                    var new_line = CreateNewLine(pt1, pt2);
                    if (line.AttribID > 0)
                    {
                        physical_curves.Add(new GmshPhysicalCurve(new List<GmshCurvilinearEntity>(new GmshCurvilinearEntity[1] { new_line }), line.AttribID));
                    }
                }
            }

            foreach (var arc in geometry.Arcs)
            {
                var startPt = FindPoint(arc.StartPt);
                var endPt = FindPoint(arc.EndPt);
                var centerPt = FindPoint(arc.Center);
                if (centerPt is null)
                {
                    centerPt = CreateNewPoint(arc.Center.x, arc.Center.y, 0);
                }

                GmshArc new_arc;
                if (arc.SweepAngle >= 0)
                {
                    new_arc = CreateNewArc(startPt, centerPt, endPt);
                }
                else
                {
                    new_arc = CreateNewArc(endPt, centerPt, startPt);
                }
                if (arc.AttribID > 0)
                {
                    physical_curves.Add(new GmshPhysicalCurve(new List<GmshCurvilinearEntity>(new GmshCurvilinearEntity[1] { new_arc }), arc.AttribID));
                }
            }

            foreach (var loop in geometry.LineLoops)
            {
                List<GmshCurvilinearEntity> boundary = new List<GmshCurvilinearEntity>();
                foreach (var segment in loop.Boundary)
                {
                    if (segment is GeomLine)
                    {
                        var line = (GeomLine)segment;
                        var gmshLine = FindLine(line);
                        if (gmshLine == null) System.Diagnostics.Debugger.Break();
                        boundary.Add(gmshLine);
                    }
                    else if (segment is GeomArc)
                    {
                        var arc = (GeomArc)segment;
                        var gmshArc = FindArc(arc);
                        if (gmshArc == null) System.Diagnostics.Debugger.Break();
                        boundary.Add(gmshArc);
                    }
                }
                if (boundary.Count == 0) System.Diagnostics.Debugger.Break();
                CreateNewCurveLoop(boundary);
                if (loop.AttribID > 0)
                {
                    physical_curves.Add(new GmshPhysicalCurve(boundary, loop.AttribID));
                }
            }

            foreach (var surface in geometry.Surfaces)
            {
                var boundary = FindCurveLoop(surface.Boundary);
                Debug.Assert(boundary != null);
                List<GmshCurveLoop> holes = new List<GmshCurveLoop>();
                foreach (var hole in surface.Holes)
                {
                    if (hole is not null)
                    {
                        holes.Add(FindCurveLoop(hole));
                    }
                }
                var new_surface = CreateNewSurface(boundary, holes);
                if (surface.AttribID > 0)
                {
                    physical_surfaces.Add(new GmshPhysicalSurface(new_surface, surface.AttribID));
                }
            }
        }

        public void writeFile()
        {
            StreamWriter sw = File.CreateText(Filename);
            sw.WriteLine($"lc = {lc};");
            sw.WriteLine("Mesh.ElementOrder = " + ElementOrder + ";");
            if (false)
            {
                int point_ID = 0;
                foreach (GmshPoint point in points)
                {
                    point_ID++;
                    point.ID = point_ID;
                    point.Write(sw);
                }

                int line_ID = 0;
                foreach (GmshLine line in lines)
                {
                    line_ID++;
                    line.ID = line_ID;
                    line.Write(sw);
                }

                int curve_loop_ID = 0;
                foreach (GmshCurveLoop curve_loop in curve_loops)
                {
                    curve_loop_ID++;
                    curve_loop.ID = curve_loop_ID;
                    curve_loop.Write(sw);
                }

                sw.Write("Plane Surface (1) = {1, ");
                for (int i = 2; i <= curve_loop_ID; i++)
                {
                    sw.Write("{0}", i);
                    if (i < curve_loop_ID)
                    {
                        sw.Write(", ");
                    }
                }
                sw.WriteLine("};");

                for (int i = 2; i <= curve_loop_ID; i++)
                {
                    sw.WriteLine("Plane Surface ({0}) = {{{1}}};", i, i);
                }

                sw.WriteLine("Physical Point (1) = {1};");

                for (int i = 1; i <= curve_loop_ID; i++)
                {
                    sw.WriteLine("Physical Surface ({0}) = {{{1}}};", i + 1, i);
                }
            }

            foreach (GmshPoint point in points)
            {
                point.Write(sw);
            }

            foreach (GmshLine line in lines)
            {
                line.Write(sw);
            }

            foreach (GmshArc arc in arcs)
            { 
                arc.Write(sw); 
            }

            foreach (GmshCurveLoop curve_loop in curve_loops)
            {
                curve_loop.Write(sw);
            }

            foreach (GmshPlaneSurface surface in plane_surfaces)
            {
                surface.Write(sw);
            }

            foreach (GmshPhysicalCurve curve in physical_curves)
            {
                curve.Write(sw);
            }

            foreach (GmshPhysicalSurface surface in physical_surfaces)
            {
                surface.Write(sw);
            }

            sw.WriteLine("Mesh.MshFileVersion = 2;");
            sw.Close();
        }
    }

    public class GmshPoint
    {
        public double x, y, z;
        public int ID;
        public double lc = 0.4;

        public GmshPoint(double in_x, double in_y, double in_z, int in_ID=-9999, double in_lc=0.4)
        {
            x = in_x;
            y = in_y;
            z = in_z;
            ID = in_ID;
            lc = in_lc;
        }

        public void Write(StreamWriter sw){
            sw.WriteLine("Point ({0}) = {{{1}, {2}, {3}, {4}}};", ID, x, y, z, lc);
        }

        public bool Equals(GmshPoint otherPt)
        {
            if (x.AlmostEqual(otherPt.x) && y.AlmostEqual(otherPt.y))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }

    public abstract class GmshCurvilinearEntity 
    {
        public GmshPoint StartPt, EndPt;
        public int ID;
    }

    public class GmshLine : GmshCurvilinearEntity
    {
        public GmshLine(GmshPoint in_pt1, GmshPoint in_pt2){
            StartPt = in_pt1;
            EndPt = in_pt2;
            ID = -9999;
        }

        public void Write(StreamWriter sw){
            sw.WriteLine("Line ({0}) = {{{1}, {2}}};", ID, StartPt.ID, EndPt.ID);
        }

    }

    public class GmshArc : GmshCurvilinearEntity
    {
        public GmshPoint CenterPt;

        public GmshArc(GmshPoint in_startPt, GmshPoint in_centerPt, GmshPoint in_endPt)
        {
            StartPt = in_startPt;
            CenterPt = in_centerPt;
            EndPt = in_endPt;
            ID = -9999;
        }

        public void Write(StreamWriter sw)
        {
            sw.WriteLine("Circle ({0}) = {{{1}, {2}, {3}}};", ID, StartPt.ID, CenterPt.ID, EndPt.ID);
        }
    }

    public class GmshCurveLoop
    {
        public List<GmshCurvilinearEntity> segments;
        public int ID;

        public GmshCurveLoop(List<GmshCurvilinearEntity> in_segs){

            segments = in_segs;
        }

        public void Write(StreamWriter sw){
            var orientations = DetermineLineSegmentOrientations();
            sw.Write("Curve Loop ({0}) = {{", ID);
            bool firstSeg = true;
            int i = 0;
            foreach (var seg in segments) {
                if(!firstSeg){
                    sw.Write(", ");
                }else{
                    firstSeg = false;
                }
                sw.Write("{0}", seg.ID * orientations[i]);
                i++;
            }
            sw.WriteLine("};");
        }

        public bool IsMatchingLoop(GeomLineLoop in_loop)
        {
            foreach (var in_seg in in_loop.Boundary)
            {
                bool foundMatchingSegment = false;
                foreach (var seg in segments)
                {
                    if (in_seg is GeomLine in_line && seg is GmshLine line)
                    {
                        if ((in_line.pt1.x.AboutEquals(line.StartPt.x) && in_line.pt1.y.AboutEquals(line.StartPt.y) && in_line.pt2.x.AboutEquals(line.EndPt.x) && in_line.pt2.y.AboutEquals(line.EndPt.y)) ||
                            (in_line.pt2.x.AboutEquals(line.StartPt.x) && in_line.pt2.y.AboutEquals(line.StartPt.y) && in_line.pt1.x.AboutEquals(line.EndPt.x) && in_line.pt1.y.AboutEquals(line.EndPt.y)))
                        {
                            foundMatchingSegment = true;
                            break;
                        }
                    }
                    else if (in_seg is GeomArc in_arc && seg is GmshArc arc)
                    {
                        if ((in_arc.StartPt.x.AboutEquals(arc.StartPt.x) && in_arc.StartPt.y.AboutEquals(arc.StartPt.y) && in_arc.EndPt.x.AboutEquals(arc.EndPt.x) && in_arc.EndPt.y.AboutEquals(arc.EndPt.y)) ||
                            (in_arc.EndPt.x.AboutEquals(arc.StartPt.x) && in_arc.EndPt.y.AboutEquals(arc.StartPt.y) && in_arc.StartPt.x.AboutEquals(arc.EndPt.x) && in_arc.StartPt.y.AboutEquals(arc.EndPt.y)))
                        {
                            foundMatchingSegment = true;
                            break;
                        }
                    }
                }
                if (!foundMatchingSegment) return false;
            }
            return true;
        }

        private List<int> DetermineLineSegmentOrientations()
        {
            int n = segments.Count;
            List<int> orientations = new List<int>();

            //// This function sorts the edges in an edge loop; if reorient is set, it
            //// reorients the edges (and creates reversed edges if necessary). The routine
            //// also detects subloops if reorient is not set; this is useful for writing
            //// general scriptable surface generation in complex cases.
            //GmshCurvilinearEntity c, c0, c1, c2;

            //var temp = new List<GmshCurvilinearEntity>(n);

            //for (int i = 0; i < n; i++)
            //{
            //    int _j;
            //    List_Read(edges, i, &j);
            //    if ((c = FindCurve(j)))
            //    {
            //        temp.Add(c);
            //        if (c->Typ == MSH_SEGM_DISCRETE)
            //        {
            //            Msg::Debug("Aborting curve loop sort for discrete curve: "
            //                       "let's hope you know what you're doing ;-)");
            //            return true;
            //        }
            //    }
            //    else
            //    {
            //        Msg::Debug("Unknown curve %d, aborting curve loop sort: "
              
            //                   "let's hope you know what you're doing ;-)",
            //                   j);
            //        return true;
            //    }
            //}
            //bool reorient = true;
            //var edges = new List<GmshCurvilinearEntity>();

            //if (temp.Count == 0) return true;

            //bool ok = true;
            //int j = 0, k = 0;
            //c0 = c1 = temp[0];
            //edges.Add(c1);
            ////List_PSuppress(temp, 0);
            //while (edges.Count < n)
            //{
            //    for (int i = 0; i < temp.Count; i++)
            //    {
            //        c2 = temp[i];
            //        if (reorient && c1.EndPt == c2.EndPt)
            //        {
            //            //Flip c2
            //            Curve* c2R = FindCurve(-c2->Num);
            //            if (!c2R)
            //            {
            //                Msg::Debug("Creating reversed curve -%d", -c2->Num);
            //                c2R = CreateReversedCurve(c2);
            //            }
            //            c2 = c2R;
            //        }
            //        if (c1.EndPt == c2.StartPt)
            //        {
            //            edges.Add(c2);
            //            c1 = c2;
            //            if (c2.EndPt == c0.StartPt)
            //            {
            //                if (List_Nbr(temp))
            //                {
            //                    //Msg::Info("Starting subloop %d in curve loop %d (are you sure about this?)",++k, num);
            //                    c0 = c1 = temp[0];
            //                    edges.Add(c1);
            //                }
            //            }
            //            break;
            //        }
            //    }
            //    if (j++ > n)
            //    {
            //        Msg::Error("Curve loop %d is wrong", num);
            //        ok = false;
            //        break;
            //    }
            //}
            

            //orientations.Add(1); //First segment will be arbitrarily 1
            int currDirection = 1;
            var currSegment = segments[0];
            var nextSegment = segments[1];
            // First segment, determine direction
            if (currSegment.StartPt.Equals(nextSegment.StartPt) || currSegment.StartPt.Equals(nextSegment.EndPt))
            {
                currDirection = -1;  
            }
            else if (currSegment.EndPt.Equals(nextSegment.StartPt) || currSegment.EndPt.Equals(nextSegment.EndPt))
            {
                currDirection = 1;
            }
            else
            {
                throw new Exception("Line Loop first segment is not connected to second segment.");
            }
            orientations.Add(currDirection);

            for (int i = 0; i < n - 1; i++)
            {
                currSegment = segments[i];
                nextSegment = segments[i + 1];

                if (currDirection > 0) //Proceed in current direction
                {
                    if (currSegment.EndPt.Equals(nextSegment.StartPt))
                    {
                        orientations.Add(1);
                    }
                    else if (currSegment.EndPt.Equals(nextSegment.EndPt))
                    {
                        orientations.Add(-1);
                    }
                    else
                    {
                        throw new Exception("Line Loop segments do not appear to be continuous.");
                    }
                }
                else //currSegment is flipped
                {
                    if (currSegment.StartPt.Equals(nextSegment.StartPt))
                    {
                        orientations.Add(1);
                    }
                    else if (currSegment.StartPt.Equals(nextSegment.EndPt))
                    {
                        orientations.Add(-1);
                    }
                    else
                    {
                        throw new Exception("Line Loop segments do not appear to be continuous.");
                    }
                }
                currDirection = orientations[i+1];
            }
            //orientations.Add(1);
            

            return orientations;
        }

        private List<int> DetermineLineSegmentOrientationsOld()
        {
            int n = segments.Count;
            List<int> orientations = new List<int>();

            // Calculate the total signed area of the polygon
            double signedArea = 0;
            for (int i = 0; i < n; i++)
            {
                var segment = segments[i];
                signedArea += (segment.EndPt.x - segment.StartPt.x) * (segment.EndPt.y + segment.StartPt.x);
            }

            // Handle degenerate case with no area, which can occur with a semi-circle, since we approximate the arc with a line segment
            if (signedArea != 0)
            {
                // Determine the orientation of each line segment
                for (int i = 0; i < n; i++)
                {
                    var segment = segments[i];
                    double cross = (segment.EndPt.x - segment.StartPt.x) * (segments[(i + 1) % n].StartPt.y - segment.StartPt.y) -
                                 (segments[(i + 1) % n].StartPt.x - segment.StartPt.x) * (segment.EndPt.y - segment.StartPt.y);

                    orientations.Add(cross == 0 ? Math.Sign(signedArea) : Math.Sign(cross) * Math.Sign(signedArea));
                }
            }
            else
            {
                //orientations.Add(1); //First segment will be arbitrarily 1
                int currDirection = 1;
                for (int i = 0; i < n - 1; i++)
                {
                    var currSegment = segments[i];
                    var nextSegment = segments[i+1];
                    
                    if (currDirection > 0) //Proceed in current direction
                    {
                        if (currSegment.EndPt.Equals(nextSegment.StartPt))
                        {
                            orientations.Add(1);
                        }
                        else if (currSegment.EndPt.Equals(nextSegment.EndPt))
                        {
                            orientations.Add(-1);
                        }
                    }
                    else //currSegment is flipped
                    {
                        if (currSegment.StartPt.Equals(nextSegment.StartPt))
                        {
                            orientations.Add(1);
                        }
                        else if (currSegment.StartPt.Equals(nextSegment.EndPt))
                        {
                            orientations.Add(-1);
                        }
                    }
                    currDirection = orientations[i];
                }
                orientations.Add(1);
            }

            return orientations;
        }

    }

    public class GmshPlaneSurface
    {
        public int ID;
        public GmshCurveLoop boundary;
        public List<GmshCurveLoop> holes;

        public GmshPlaneSurface(GmshCurveLoop in_boundary, List<GmshCurveLoop> in_holes)
        {
            Debug.Assert(in_boundary != null);
            boundary = in_boundary;
            holes = in_holes;
        }

        public void Write(StreamWriter sw)
        {
            if (holes.Count == 0)
            {
                sw.WriteLine("Plane Surface ({0}) = {{{1}}};", ID, boundary.ID);
            }
            else
            {
                sw.WriteLine("Plane Surface ({0}) = {{{1}, {2}}};", ID, boundary.ID, String.Join(", ", holes.Select(x => x.ID)));
            }
        }
    }

    public class GmshPhysicalCurve
    {
        public int ID;
        public List<GmshCurvilinearEntity> curves;

        public GmshPhysicalCurve(List<GmshCurvilinearEntity> _curves, int _ID)
        {
            curves = _curves;
            ID = _ID;
        }

        public void Write(StreamWriter sw)
        {
            if (curves.Count == 1)
            {
                sw.WriteLine("Physical Curve ({0}) = {{{1}}};", ID, curves[0].ID);
            }
            else
            {
                sw.WriteLine("Physical Curve ({0}) = {{{1}}};", ID, String.Join(", ", curves.Select(x => x.ID)));
            }
        }

    }

    public class GmshPhysicalSurface
    {
        public int ID;
        public List<GmshPlaneSurface> surfaces;

        public GmshPhysicalSurface(GmshPlaneSurface surface, int _ID)
        {
            surfaces = new List<GmshPlaneSurface>();
            surfaces.Add(surface);
            ID = _ID;
        }

        public void Write(StreamWriter sw)
        {
            if (surfaces.Count == 1)
            {
                sw.WriteLine("Physical Surface ({0}) = {{{1}}};", ID, surfaces[0].ID);
            }
            else
            {
                sw.WriteLine("Physical Surface ({0}) = {{{1}}};", ID, String.Join(", ", surfaces.Select(x => x.ID)));
            }
        }

    }

}