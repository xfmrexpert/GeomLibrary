// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TDAP
{
    public enum FEMMLengthUnit
    {
        meters,
        centimeters,
        millimeters,
        inches
    }

    public enum FEMMProblemType
    {
        planar,
        axisymmetric
    }

    public enum FEMMBdryType
    {
        prescribedA=1,
        smallSkinDepth=2,
        mixed=3,
        strategicDualImage=4,
        periodic=5,
        antiPeriodic=6
    }

    public static class EnumerationExtensions
    {
        public static string AsText<T>(this T value) where T : Enum
        {
            return Enum.GetName(typeof(T), value);
        }
    }

    public class FEMMFile
    {
        public double Format { get; set; } = 4.0;
        public double Frequency { get; set; } = 60.0;
        public double Precision { get; set; } = 1.0e-4;
        public double MinAngle { get; set; } = 20.0;
        public double Depth { get; set; } = 1.0;
        public FEMMLengthUnit LengthUnits { get; set; } = FEMMLengthUnit.meters;
        public string Coordinates { get; set; } = "cartesian";
        public FEMMProblemType ProblemType { get; set; } = FEMMProblemType.axisymmetric;
        public double ExtZo {  get; set; }
        public double ExtRo { get; set; }
        public double ExtRi { get; set; }
        public string Comment { get; set; } = "";

        List<FEMMPointProp> PointProps { get; set; } = new List<FEMMPointProp>();
        List<FEMMBdryProp> BdryProps { get; set; } = new List<FEMMBdryProp>();
        List<FEMMBlockProp> BlockProps { get; set; } = new List<FEMMBlockProp>();
        List<FEMMCircuitProp> CircuitProps { get; set; } = new List<FEMMCircuitProp>();

        List<FEMMPoint> Points { get; set; } = new List<FEMMPoint>();
        List<FEMMSegment> Segments { get; set; } = new List<FEMMSegment>();
        List<FEMMArcSegment> ArcSegments { get; set; } = new List<FEMMArcSegment>();
        List<FEMMHole> Holes { get; set; } = new List<FEMMHole>();
        List<FEMMBlockLabel> BlockLabels { get; set; } = new List<FEMMBlockLabel>();

        public void CreateFromGeometry(Geometry geometry, Dictionary<int, int> blockMap, Dictionary<int, int> circMap)
        {
            Points.Clear();
            Segments.Clear();
            ArcSegments.Clear();

            foreach (var point in geometry.Points)
            {
                CreateNewPoint(point.x, point.y);
            }

            foreach (var line in geometry.Lines)
            {
                CreateNewSegment(line.pt1, line.pt2);
            }

            foreach (var arc in geometry.Arcs)
            {
                CreateNewArcSegment(arc.EndPt, arc.StartPt, arc.SweepAngle*180.0/Math.PI);
            }

            foreach (var loop in geometry.LineLoops)
            {
                
            }

            foreach (var surface in geometry.Surfaces)
            {
                CreateNewBlockLabel(surface, blockMap, circMap);
            }
        }

        public FEMMPoint CreateNewPoint(double x, double y)
        {
            var newPt = new FEMMPoint(x, y);
            Points.Add(newPt);
            return newPt;
        }

        public int FindPoint(double x, double y)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                var point = Points[i];
                if (point.X == x && point.Y == y)
                {
                    return i;
                }
            }
            return -1;
        }

        public FEMMSegment CreateNewSegment(GeomPoint pt1, GeomPoint pt2, int bdryID = 0)
        {
            int idx1 = FindPoint(pt1.x, pt1.y);
            int idx2 = FindPoint(pt2.x, pt2.y);
            var newSeg = new FEMMSegment(idx1, idx2, -1, bdryID);
            Segments.Add(newSeg);
            return newSeg;
        }

        public FEMMArcSegment CreateNewArcSegment(GeomPoint startPt, GeomPoint endPt, double sweepAngle)
        {
            int idxSt = FindPoint(startPt.x, startPt.y);
            int idxEnd = FindPoint(endPt.x, endPt.y);
            var newArc = new FEMMArcSegment(idxSt, idxEnd, sweepAngle);
            ArcSegments.Add(newArc);
            return newArc;
        }

        public FEMMBlockLabel CreateNewBlockLabel(GeomSurface surface, Dictionary<int, int> blockMap, Dictionary<int, int> circMap)
        {
            GeomPoint pt = surface.GetRandomPointInSurface();
            int blockID = 0;
            int circID = 0;
            if (blockMap.ContainsKey(surface.AttribID))
            {
                blockID = blockMap[surface.AttribID];
            }
            if (circMap.ContainsKey(surface.AttribID))
            {
                //circID = circMap[surface.AttribID];
            }

            var newBlockLabel = new FEMMBlockLabel(pt.x, pt.y, blockID, 0, circID, 0, 0, 1, false);
            BlockLabels.Add(newBlockLabel);
            return newBlockLabel;
        }

        public int CreateNewBlockProp(string name)
        {
            var newBlockProp = new FEMMBlockProp() { BlockName = name };
            BlockProps.Add(newBlockProp);
            return BlockProps.Count;
        }

        public void ToFile(string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine($"[Format]      =  {Format}");
                sw.WriteLine($"[Frequency]   =  {Frequency}");
                sw.WriteLine($"[Precision]   =  {Precision}");
                sw.WriteLine($"[MinAngle]    =  {MinAngle}");
                sw.WriteLine($"[DoSmartMesh] =  {1}");
                sw.WriteLine($"[Depth]       =  {Depth}");
                sw.WriteLine($"[LengthUnits] =  {LengthUnits.AsText()}");
                sw.WriteLine($"[ProblemType] =  {ProblemType.AsText()}");
                sw.WriteLine($"[Coordinates] =  {Coordinates}");
                sw.WriteLine($"[ACSolver]    =  {0}");
                sw.WriteLine($"[PrevType]    =  {0}");
                sw.WriteLine($"[PrevSoln]    =  \"\"");
                sw.WriteLine($"[Comment]     =  \"{Comment}\"");

                sw.WriteLine($"[PointProps]   = {PointProps.Count}");
                foreach (var pointProp in PointProps)
                {
                    sw.Write(pointProp.ToString());
                }

                sw.WriteLine($"[BdryProps]  = {BdryProps.Count}");
                foreach (var bdryProp in BdryProps)
                {
                    sw.Write(bdryProp.ToString());
                }

                sw.WriteLine($"[BlockProps]  = {BlockProps.Count}");
                foreach (var blockProp in BlockProps)
                {
                    sw.Write(blockProp.ToString());
                }

                sw.WriteLine($"[CircuitProps]   = {CircuitProps.Count}");
                foreach (var circuitProp in CircuitProps)
                {
                    sw.Write(circuitProp.ToString());
                }

                sw.WriteLine($"[NumPoints] = {Points.Count}");
                foreach (var pt in Points)
                {
                    sw.WriteLine(pt.ToString());
                }

                sw.WriteLine($"[NumSegments] = {Segments.Count}");
                foreach (var seg in Segments)
                {
                    sw.WriteLine(seg.ToString());
                }

                sw.WriteLine($"[NumArcSegments] = {ArcSegments.Count}");
                foreach (var arc in ArcSegments)
                {
                    sw.WriteLine(arc.ToString());
                }

                sw.WriteLine($"[NumHoles] = {Holes.Count}");
                foreach (var hole in Holes)
                {
                    sw.WriteLine(hole.ToString());
                }

                sw.WriteLine($"[NumBlockLabels] = {BlockLabels.Count}");
                foreach (var block in BlockLabels)
                {
                    sw.WriteLine(block.ToString());
                }
            }
        }

        // Replaces the portion of the code that writes .poly and .pbc files.
        //public bool GeneratePolyAndPbcFiles()
        //{
        //    // local variables
        //    int i, j, k, l, t;
        //    double z, R, dL;
        //    Complex a0, a1, a2, c;

        //    // Suppose bSmartMesh is read from "theApp.session_SmartMesh"
        //    int bSmartMesh = MyApp.session_SmartMesh;
        //    if (bSmartMesh < 0) bSmartMesh = SmartMesh;

        //    // We’ll create new lists for the meshing process:
        //    List<FEMMPoint> nodelst = new List<FEMMPoint>();
        //    List<FEMMSegment> linelst = new List<FEMMSegment>();

        //    nodelst.Clear();
        //    linelst.Clear();

        //    // ===============================
        //    // 1) Compute dL for “kludge fine meshing near corners”
        //    // ===============================
        //    z = 0.0;
        //    for (i = 0; i < Segments.Count; i++)
        //    {
        //        a0 = new Complex(Points[Segments[i].StartPt].X, Points[Segments[i].StartPt].Y);
        //        a1 = new Complex(Points[Segments[i].EndPt].X, Points[Segments[i].EndPt].Y);
        //        double length = (a1 - a0).Magnitude;   // = abs(a1 - a0)
        //        z += (length / Segments.Count);
        //    }
        //    dL = z / LineFraction;

        //    // ===============================
        //    // 2) Copy node list as-is
        //    // ===============================
        //    for (i = 0; i < Points.Count; i++)
        //    {
        //        nodelst.Add(Points[i]);
        //    }

        //    // ===============================
        //    // 3) Discretize input segments
        //    // ===============================
        //    for (i = 0; i < Segments.Count; i++)
        //    {
        //        a0 = new Complex(Points[Segments[i].StartPt].X, Points[Segments[i].StartPt].Y);
        //        a1 = new Complex(Points[Segments[i].EndPt].X, Points[Segments[i].EndPt].Y);

        //        double length = (a1 - a0).Magnitude;

        //        // if MaxSideLength = -1 => k=1
        //        if (Segments[i].BoundaryMarker == null) Segments[i].BoundaryMarker = "";

        //        if (/* replace with your condition for MaxSideLength == -1 */ false)
        //        {
        //            k = 1;
        //        }
        //        else
        //        {
        //            double maxSideLength = /* you must store or retrieve from the segment somehow */ 10.0;
        //            k = (int)Math.Ceiling(length / maxSideLength);
        //        }

        //        if (k == 1)
        //        {
        //            // default condition
        //            if ((length < (3.0 * dL)) || (bSmartMesh == 0))
        //            {
        //                // line is too short to subdivide or meshing turned off
        //                linelst.Add(Segments[i]);
        //            }
        //            else
        //            {
        //                // add extra points near the ends
        //                FEMMSegment segm = new FEMMSegment
        //                {
        //                    BoundaryMarker = Segments[i].BoundaryMarker
        //                };

        //                // we subdivide into 3 segments around the ends
        //                for (j = 0; j < 3; j++)
        //                {
        //                    if (j == 0)
        //                    {
        //                        // first extra point
        //                        a2 = a0 + dL * (a1 - a0) / length;

        //                        CNode node = new CNode
        //                        {
        //                            x = a2.Real,
        //                            y = a2.Imaginary
        //                        };
        //                        l = nodelst.Count;
        //                        nodelst.Add(node);

        //                        segm.n0 = linelist[i].n0;
        //                        segm.n1 = l;
        //                        linelst.Add(new CSegment
        //                        {
        //                            n0 = segm.n0,
        //                            n1 = segm.n1,
        //                            BoundaryMarker = segm.BoundaryMarker
        //                        });
        //                    }
        //                    else if (j == 1)
        //                    {
        //                        // second extra point
        //                        a2 = a1 + dL * (a0 - a1) / length;

        //                        CNode node = new CNode
        //                        {
        //                            x = a2.Real,
        //                            y = a2.Imaginary
        //                        };
        //                        l = nodelst.Count;
        //                        nodelst.Add(node);

        //                        segm.n0 = l - 1;
        //                        segm.n1 = l;
        //                        linelst.Add(new CSegment
        //                        {
        //                            n0 = segm.n0,
        //                            n1 = segm.n1,
        //                            BoundaryMarker = segm.BoundaryMarker
        //                        });
        //                    }
        //                    else
        //                    {
        //                        // connect last new point to the original endpoint
        //                        l = nodelst.Count - 1;
        //                        segm.n0 = l;
        //                        segm.n1 = linelist[i].n1;
        //                        linelst.Add(new CSegment
        //                        {
        //                            n0 = segm.n0,
        //                            n1 = segm.n1,
        //                            BoundaryMarker = segm.BoundaryMarker
        //                        });
        //                    }
        //                }
        //            }
        //        }
        //        else
        //        {
        //            // subdivide into k segments
        //            CSegment segm = new CSegment
        //            {
        //                BoundaryMarker = linelist[i].BoundaryMarker
        //            };

        //            for (j = 0; j < k; j++)
        //            {
        //                double fraction = (double)(j + 1) / (double)k;
        //                a2 = a0 + (a1 - a0) * fraction;

        //                CNode node = new CNode
        //                {
        //                    x = a2.Real,
        //                    y = a2.Imaginary
        //                };

        //                if (j == 0)
        //                {
        //                    // first subdivision
        //                    int newNodeIndex = nodelst.Count;
        //                    nodelst.Add(node);

        //                    segm.n0 = linelist[i].n0;
        //                    segm.n1 = newNodeIndex;
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = segm.n0,
        //                        n1 = segm.n1,
        //                        BoundaryMarker = segm.BoundaryMarker
        //                    });
        //                }
        //                else if (j == (k - 1))
        //                {
        //                    // last subdivision
        //                    int newNodeIndex = nodelst.Count - 1;
        //                    segm.n0 = newNodeIndex;
        //                    segm.n1 = linelist[i].n1;
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = segm.n0,
        //                        n1 = segm.n1,
        //                        BoundaryMarker = segm.BoundaryMarker
        //                    });
        //                }
        //                else
        //                {
        //                    // intermediate subdivision
        //                    int newNodeIndex = nodelst.Count;
        //                    int prevNodeIndex = newNodeIndex - 1; // after adding

        //                    nodelst.Add(node);

        //                    segm.n0 = prevNodeIndex;
        //                    segm.n1 = newNodeIndex;
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = segm.n0,
        //                        n1 = segm.n1,
        //                        BoundaryMarker = segm.BoundaryMarker
        //                    });
        //                }
        //            }
        //        }
        //    }

        //    // ===============================
        //    // 4) Discretize input arc segments
        //    // ===============================
        //    for (i = 0; i < arclist.Count; i++)
        //    {
        //        // set “mySideLength” from “MaxSideLength”
        //        arclist[i].mySideLength = arclist[i].MaxSideLength;

        //        // number of subdivisions
        //        int n0Index = arclist[i].n0;
        //        int n1Index = arclist[i].n1;

        //        a2 = new Complex(nodelist[n0Index].x, nodelist[n0Index].y);

        //        // figure out how many pieces
        //        k = (int)Math.Ceiling(arclist[i].ArcLength / arclist[i].MaxSideLength);

        //        // boundary marker
        //        CSegment arcSeg = new CSegment();
        //        arcSeg.BoundaryMarker = arclist[i].BoundaryMarker;

        //        // get circle center & radius
        //        GetCircle(arclist[i], out c, out R);

        //        // a1 = exp( i * (arcLength / k in radians) )
        //        // arclist[i].ArcLength is presumably in degrees, so multiply by π/180
        //        // If arcLength is in degrees:
        //        double anglePerSub = arclist[i].ArcLength * Math.PI / (k * 180.0);
        //        Complex multiplier = Complex.Exp(Complex.ImaginaryOne * anglePerSub);

        //        if (k == 1)
        //        {
        //            // just one segment connecting n0 => n1
        //            arcSeg.n0 = n0Index;
        //            arcSeg.n1 = n1Index;
        //            linelst.Add(arcSeg);
        //        }
        //        else
        //        {
        //            for (j = 0; j < k; j++)
        //            {
        //                // rotate vector (a2 - c) by multiplier
        //                a2 = (a2 - c) * multiplier + c;

        //                CNode node = new CNode
        //                {
        //                    x = a2.Real,
        //                    y = a2.Imaginary
        //                };

        //                if (j == 0)
        //                {
        //                    int newIndex = nodelst.Count;
        //                    nodelst.Add(node);

        //                    arcSeg.n0 = n0Index;
        //                    arcSeg.n1 = newIndex;
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = arcSeg.n0,
        //                        n1 = arcSeg.n1,
        //                        BoundaryMarker = arcSeg.BoundaryMarker
        //                    });
        //                }
        //                else if (j == (k - 1))
        //                {
        //                    int lastIndex = nodelst.Count - 1;
        //                    arcSeg.n0 = lastIndex;
        //                    arcSeg.n1 = n1Index;
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = arcSeg.n0,
        //                        n1 = arcSeg.n1,
        //                        BoundaryMarker = arcSeg.BoundaryMarker
        //                    });
        //                }
        //                else
        //                {
        //                    int newIndex = nodelst.Count;
        //                    nodelst.Add(node);

        //                    arcSeg.n0 = newIndex - 1;   // the previously added node
        //                    arcSeg.n1 = newIndex;      // this new node
        //                    linelst.Add(new CSegment
        //                    {
        //                        n0 = arcSeg.n0,
        //                        n1 = arcSeg.n1,
        //                        BoundaryMarker = arcSeg.BoundaryMarker
        //                    });
        //                }
        //            }
        //        }
        //    }

        //    // ===============================
        //    // 5) Build the output .poly filename
        //    // ===============================
        //    string pn = GetPathName();
        //    int dotPos = pn.LastIndexOf('.');
        //    if (dotPos < 0) dotPos = pn.Length; // if no extension
        //    string plyname = pn.Substring(0, dotPos) + ".poly";

        //    // Try to open the .poly file
        //    StreamWriter writer;
        //    try
        //    {
        //        writer = new StreamWriter(plyname);
        //    }
        //    catch
        //    {
        //        Console.WriteLine("Couldn't write to specified .poly file");
        //        return false;
        //    }

        //    // ===============================
        //    // 6) Write out the node list
        //    // ===============================
        //    writer.WriteLine("{0}\t2\t0\t1", nodelst.Count);

        //    for (i = 0; i < nodelst.Count; i++)
        //    {
        //        // find property index
        //        t = 0;
        //        for (j = 0; j < BdryProps.Count; j++)
        //        {
        //            if (PointProps[j].PointName == nodelst[i].BoundaryMarker)
        //            {
        //                t = j + 2;
        //                break;
        //            }
        //        }
        //        writer.WriteLine("{0}\t{1:F17}\t{2:F17}\t{3}", i, nodelst[i].X, nodelst[i].Y, t);
        //    }

        //    // ===============================
        //    // 7) Write out segment list
        //    // ===============================
        //    writer.WriteLine("{0}\t1", linelst.Count);

        //    for (i = 0; i < linelst.Count; i++)
        //    {
        //        t = 0;
        //        for (j = 0; j < lineproplist.Count; j++)
        //        {
        //            if (BdryProps[j].BdryName == linelst[i].BoundaryMarker)
        //            {
        //                t = -(j + 2);
        //                break;
        //            }
        //        }
        //        writer.WriteLine("{0}\t{1}\t{2}\t{3}", i, linelst[i].n0, linelst[i].n1, t);
        //    }

        //    // ===============================
        //    // 8) Write out list of holes (block labels with <No Mesh>)
        //    // ===============================
        //    int holeCount = 0;
        //    for (i = 0; i < blocklist.Count; i++)
        //    {
        //        if (blocklist[i].BlockType == "<No Mesh>") holeCount++;
        //    }
        //    writer.WriteLine(holeCount);

        //    int holeIndex = 0;
        //    for (i = 0; i < blocklist.Count; i++)
        //    {
        //        if (blocklist[i].BlockType == "<No Mesh>")
        //        {
        //            writer.WriteLine("{0}\t{1:F17}\t{2:F17}",
        //                             holeIndex, blocklist[i].x, blocklist[i].y);
        //            holeIndex++;
        //        }
        //    }

        //    // ===============================
        //    // 9) Calculate a default mesh size for other block labels
        //    // ===============================
        //    double DefaultMeshSize;
        //    if (nodelst.Count > 1)
        //    {
        //        // find bounding box
        //        double minX = nodelst[0].x, maxX = nodelst[0].x;
        //        double minY = nodelst[0].y, maxY = nodelst[0].y;
        //        for (k = 1; k < nodelst.Count; k++)
        //        {
        //            if (nodelst[k].x < minX) minX = nodelst[k].x;
        //            if (nodelst[k].x > maxX) maxX = nodelst[k].x;
        //            if (nodelst[k].y < minY) minY = nodelst[k].y;
        //            if (nodelst[k].y > maxY) maxY = nodelst[k].y;
        //        }

        //        double width = (maxX - minX);
        //        double height = (maxY - minY);
        //        double diag = Math.Sqrt(width * width + height * height);

        //        // For demonstration, the original code did: pow(abs(yy-xx)/BoundingBoxFraction,2)
        //        // We'll interpret that as:
        //        DefaultMeshSize = Math.Pow(diag / BoundingBoxFraction, 2.0);

        //        if (bSmartMesh == 0)
        //        {
        //            // or if !bSmartMesh => fallback
        //            DefaultMeshSize = diag;
        //        }
        //    }
        //    else
        //    {
        //        DefaultMeshSize = -1;
        //    }

        //    // ===============================
        //    // 10) Write out regional attributes for non-hole blocks
        //    // ===============================
        //    int nonHoleCount = blocklist.Count - holeCount;
        //    writer.WriteLine(nonHoleCount);

        //    int blockIndex = 0;
        //    for (i = 0; i < blocklist.Count; i++)
        //    {
        //        if (blocklist[i].BlockType != "<No Mesh>")
        //        {
        //            writer.Write("{0}\t{1:F17}\t{2:F17}\t",
        //                         blockIndex, blocklist[i].x, blocklist[i].y);
        //            // some ID (k+1 in original):
        //            writer.Write("{0}\t", blockIndex + 1);

        //            // max area if specified
        //            if ((blocklist[i].MaxArea > 0) && (blocklist[i].MaxArea < DefaultMeshSize))
        //            {
        //                writer.WriteLine("{0:F17}", blocklist[i].MaxArea);
        //            }
        //            else
        //            {
        //                writer.WriteLine("{0:F17}", DefaultMeshSize);
        //            }
        //            blockIndex++;
        //        }
        //    }

        //    writer.Close(); // done with .poly

        //    // ===============================
        //    // 11) Write out a trivial .pbc file
        //    // ===============================
        //    string pbcname = pn.Substring(0, dotPos) + ".pbc";
        //    try
        //    {
        //        using (StreamWriter sw = new StreamWriter(pbcname))
        //        {
        //            sw.WriteLine("0");
        //            sw.WriteLine("0");
        //        }
        //    }
        //    catch
        //    {
        //        MsgBox("Couldn't write to specified .pbc file");
        //        return false;
        //    }

        //    // If everything succeeded:
        //    return true;
        //}
    }

    public class FEMMPointProp
    {
        public string PointName { get; set; } = "";
        public double A_re { get; set; }
        public double A_im { get; set; }
        public double I_re { get; set; }
        public double I_im { get; set; }
    }

    public class FEMMBdryProp
    {
        public string BdryName { get; set; } = "";
        public FEMMBdryType BdryType { get; set; }
        public double Mu_ssd { get; set; }
        public double Sigma_ssd { get; set; }
        public double C0 { get; set; }
        public double C0i { get; set; }
        public double C1 { get; set; }
        public double C1i { get; set; }
        public double A_0 { get; set; }
        public double A_1 { get; set; }
        public double A_2 { get; set; }
        public double Phi { get; set; }
        public double innerangle { get; set; }
        public double outerangle { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("  <BeginBdry>");
            sb.AppendLine($"    <BdryName> = {BdryName}");
            sb.AppendLine($"    <BdryType> = {BdryType}");
            sb.AppendLine($"    <A_0> = {A_0}");
            sb.AppendLine($"    <A_1> = {A_1}");
            sb.AppendLine($"    <A_2> = {A_2}");
            sb.AppendLine($"    <Phi> = {Phi}");
            sb.AppendLine($"    <c0> = {C0}");
            sb.AppendLine($"    <c0i> = {C0i}");
            sb.AppendLine($"    <c1> = {C1}");
            sb.AppendLine($"    <c1i> = {C1i}");
            sb.AppendLine($"    <Mu_ssd> = {Mu_ssd}");
            sb.AppendLine($"    <Sigma_ssd> = {Sigma_ssd}");
            sb.AppendLine($"    <innerangle> = {innerangle}");
            sb.AppendLine($"    <outerangle> = {outerangle}");
            sb.AppendLine("  <EndBdry>");
            return sb.ToString();
        }
    }

    public class FEMMBlockProp
    {
        public string BlockName { get; set; }
        public double Mu_x { get; set; }
        public double Mu_y { get; set; }
        public double H_c { get; set; }
        public double H_cAngle { get; set; }
        public double J_re { get; set; }
        public double J_im { get; set; }
        public double Sigma { get; set; }
        public double d_lam { get; set; }
        public double Phi_h { get; set; }
        public double Phi_hx { get; set; }
        public double Phi_hy { get; set; }
        public int LamType { get; set; }
        public int LamFill { get; set; }
        public int NStrands { get; set; }
        public double WireD { get; set; }
        public int BHPoints { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("  <BeginBlock>");
            sb.AppendLine($"    <BlockName> = {BlockName}");
            sb.AppendLine($"    <Mu_x> = {Mu_x}");
            sb.AppendLine($"    <Mu_y> = {Mu_y}");
            sb.AppendLine($"    <H_c> = {H_c}");
            sb.AppendLine($"    <H_cAngle> = {H_cAngle}");
            sb.AppendLine($"    <J_re> = {J_re}");
            sb.AppendLine($"    <J_im> = {J_im}");
            sb.AppendLine($"    <Sigma> = {Sigma}");
            sb.AppendLine($"    <d_lam> = {d_lam}");
            sb.AppendLine($"    <Phi_h> = {Phi_h}");
            sb.AppendLine($"    <Phi_hx> = {Phi_hx}");
            sb.AppendLine($"    <Phi_hy> = {Phi_hy}");
            sb.AppendLine($"    <LamType> = {LamType}");
            sb.AppendLine($"    <LamFill> = {LamFill}");
            sb.AppendLine($"    <NStrands> = {NStrands}");
            sb.AppendLine($"    <WireD> = {WireD}");
            sb.AppendLine($"    <BHPoints> = {BHPoints}");
            sb.AppendLine("  <EndBlock>");
            return sb.ToString();
        }
    }

    public class FEMMCircuitProp
    {
        public string CircuitName { get; set; } = "";
        public int CircuitType { get; set; }
        public float TotalAmps_re { get; set; }
        public float TotalAmps_im { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("  <BeginCircuit>");
            sb.AppendLine($"    <CircuitName> = {CircuitName}");
            sb.AppendLine($"    <type> = {CircuitType}");
            sb.AppendLine($"    <TotalAmps_re> = {TotalAmps_re}");
            sb.AppendLine($"    <TotalAmps_im> = {TotalAmps_im}");
            sb.AppendLine("  <EndCircuit>");
            return sb.ToString();
        }
    }

    public class FEMMPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int PointProp { get; set; }
        public int GroupNum { get; set; }

        public FEMMPoint(double x, double y, int pointProp = 0, int groupNum = 0)
        {
            X = x;
            Y = y;
            PointProp = pointProp;
            GroupNum = groupNum;
        }

        public override string ToString()
        {
            return $"{X}\t{Y}\t{PointProp}\t{GroupNum}";
        }
    }

    public class FEMMSegment
    {
        public int StartPt { get; set; }
        public int EndPt { get; set; }
        public double MeshSize { get; set; }
        public int BdryProp { get; set; }
        public int HideInPost { get; set; }
        public int GroupNum  { get; set; }

        public FEMMSegment(int startPt, int endPt, double meshSize=-1, int bdryProp=0, int hideInPost=0, int groupNum=1)
        {
            StartPt = startPt;
            EndPt = endPt;
            MeshSize = meshSize;
            BdryProp = bdryProp;
            HideInPost = hideInPost;
            GroupNum = groupNum;
        }

        public override string ToString()
        {
            return $"{StartPt}\t{EndPt}\t{MeshSize}\t{BdryProp}\t{HideInPost}\t{GroupNum}";
        }
    }

    public class FEMMArcSegment
    {
        int StartPt { get; set; }
        int EndPt { get; set; }
        double ArcAngle { get; set; }
        double MaxSegment {  get; set; }
        int BdryProp { get; set; }
        int HideInPost { get; set; }
        int GroupNum { get; set; }

        public FEMMArcSegment(int startPt, int endPt, double arcAngle, double maxSegment=10, int bdryProp=0, int hideInPost=0, int groupNum=1)
        {
            StartPt = startPt;
            EndPt = endPt;
            ArcAngle = arcAngle;
            MaxSegment = maxSegment;
            BdryProp = bdryProp;
            HideInPost = hideInPost;
            GroupNum = groupNum;
        }

        public override string ToString()
        {
            return $"{StartPt}\t{EndPt}\t{ArcAngle}\t{MaxSegment}\t{BdryProp}\t{HideInPost}\t{GroupNum}";
        }
    }

    public class FEMMHole
    {
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public int GroupNum { get; set; }

        public FEMMHole(double labelX, double labelY, int groupNum)
        {
            LabelX = labelX;
            LabelY = labelY;
            GroupNum = groupNum;
        }

        public override string ToString()
        {
            return $"{LabelX}\t{LabelY}\t{GroupNum}";
        }
    }

    public class FEMMBlockLabel
    {
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public int BlockType { get; set; }
        public double MeshSize { get; set; }
        public int Circuit { get; set; } = 0;
        public double MagDir { get; set; }
        public int GroupNum { get; set; }
        public int NumTurns { get; set; } = 1;
        public bool IsExternal { get; set; } = false;

        public FEMMBlockLabel(double labelX, double labelY, int blockType, double meshSize, int circuit, double magDir, int groupNum, int numTurns, bool isExternal)
        {
            LabelX = labelX;
            LabelY = labelY;
            BlockType = blockType;
            MeshSize = meshSize;
            Circuit = circuit;
            MagDir = magDir;
            GroupNum = groupNum;
            NumTurns = numTurns;
            IsExternal = isExternal;
        }

        public override string ToString()
        {
            return $"{LabelX}\t{LabelY}\t{BlockType}\t{MeshSize}\t{Circuit}\t{MagDir}\t{GroupNum}\t{NumTurns}\t{(IsExternal ? 0 : 1)}";
        }
    }
}
