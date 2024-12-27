// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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
    }

    public class FEMMPointProp
    {
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
        int StartPt { get; set; }
        int EndPt { get; set; }
        double MeshSize { get; set; }
        int BdryProp { get; set; }
        int HideInPost { get; set; }
        int GroupNum  { get; set; }

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
