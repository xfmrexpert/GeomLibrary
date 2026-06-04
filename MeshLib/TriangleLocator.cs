using System;
using System.Collections.Generic;

namespace MeshLib
{
    /// <summary>
    /// Spatial index for locating which triangular element of a <see cref="GmshFile"/>
    /// contains a given (x, y) point. Backed by a uniform grid of triangle ids over
    /// the mesh's bounding box.
    /// </summary>
    /// <remarks>
    /// Only 2D triangle elements are indexed: Gmsh type 2 (3-node) and type 9 (6-node).
    /// For type-9 elements the 3 corner nodes (first three entries of
    /// <see cref="GmshElement.Nodes"/>) are used for the inside-test; the mid-edge
    /// nodes are ignored here. A point on a shared edge may be reported in either
    /// adjacent triangle; barycentric coordinates are clamped to the simplex on
    /// query so values are still well-defined.
    ///
    /// Typical usage from a tracer:
    /// <code>
    /// if (locator.TryLocate(x, y, hint, out var hit)) { ... hint = hit.ElementId; }
    /// </code>
    /// Passing the previous element id as <c>hint</c> turns the lookup into a cheap
    /// neighbour walk for streamline integration.
    /// </remarks>
    public sealed class TriangleLocator
    {
        /// <summary>Result of a successful point-location query.</summary>
        public readonly record struct Hit(
            int ElementId,
            int PhysicalTag,
            int N0, int N1, int N2,
            double B0, double B1, double B2);

        private readonly GmshFile _mesh;
        private readonly Dictionary<int, (double X, double Y)> _nodes;

        // Per-triangle cached data (parallel arrays, indexed by internal triangle index).
        private readonly int[] _triElementIds;
        private readonly int[] _triPhysicalTags;
        private readonly int[] _triN0;
        private readonly int[] _triN1;
        private readonly int[] _triN2;
        // Precomputed for fast barycentric: (x0, y0) and inverse of 2x2 matrix
        // [ x1-x0  x2-x0 ; y1-y0  y2-y0 ].
        private readonly double[] _triX0;
        private readonly double[] _triY0;
        private readonly double[] _triInvA, _triInvB, _triInvC, _triInvD;
        // Triangle bounding boxes (for grid binning).
        private readonly double[] _triMinX, _triMinY, _triMaxX, _triMaxY;

        // Element id -> internal triangle index (for hint lookup).
        private readonly Dictionary<int, int> _elementIdToTri;
        // Adjacency: each triangle's up-to-3 edge-sharing neighbours (by internal index, -1 if none).
        private readonly int[,] _neighbors;

        // Uniform grid.
        private readonly double _gridMinX, _gridMinY;
        private readonly double _cellW, _cellH;
        private readonly int _nx, _ny;
        private readonly int[][] _cellTris; // flattened [iy*_nx + ix] -> triangle indices

        /// <summary>Bounding box of all indexed triangles.</summary>
        public (double MinX, double MinY, double MaxX, double MaxY) Bounds =>
            (_gridMinX, _gridMinY, _gridMinX + _cellW * _nx, _gridMinY + _cellH * _ny);

        /// <summary>Number of triangles in the index.</summary>
        public int TriangleCount => _triElementIds.Length;

        public TriangleLocator(GmshFile mesh)
        {
            _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));

            _nodes = new Dictionary<int, (double, double)>(mesh.Nodes.Count);
            foreach (var n in mesh.Nodes)
                _nodes[(int)n.Id] = (n.X, n.Y);

            // First pass: count tris.
            int triCount = 0;
            foreach (var e in mesh.Elements)
                if (IsTriangle(e.Type) && e.Nodes.Count >= 3) triCount++;

            _triElementIds = new int[triCount];
            _triPhysicalTags = new int[triCount];
            _triN0 = new int[triCount];
            _triN1 = new int[triCount];
            _triN2 = new int[triCount];
            _triX0 = new double[triCount];
            _triY0 = new double[triCount];
            _triInvA = new double[triCount];
            _triInvB = new double[triCount];
            _triInvC = new double[triCount];
            _triInvD = new double[triCount];
            _triMinX = new double[triCount];
            _triMinY = new double[triCount];
            _triMaxX = new double[triCount];
            _triMaxY = new double[triCount];
            _elementIdToTri = new Dictionary<int, int>(triCount);

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

            int t = 0;
            foreach (var e in mesh.Elements)
            {
                if (!IsTriangle(e.Type) || e.Nodes.Count < 3) continue;

                int id0 = e.Nodes[0], id1 = e.Nodes[1], id2 = e.Nodes[2];
                if (!_nodes.TryGetValue(id0, out var p0) ||
                    !_nodes.TryGetValue(id1, out var p1) ||
                    !_nodes.TryGetValue(id2, out var p2))
                {
                    continue;
                }

                _triElementIds[t] = (int)e.Id;
                _triPhysicalTags[t] = e.Tags.Count > 0 ? e.Tags[0] : 0;
                _triN0[t] = id0; _triN1[t] = id1; _triN2[t] = id2;
                _triX0[t] = p0.X; _triY0[t] = p0.Y;

                // Precompute inverse of [ a b ; c d ] = [ x1-x0 x2-x0 ; y1-y0 y2-y0 ].
                double a = p1.X - p0.X;
                double b = p2.X - p0.X;
                double c = p1.Y - p0.Y;
                double d = p2.Y - p0.Y;
                double det = a * d - b * c;
                if (det == 0)
                {
                    // Degenerate; mark with NaN so it never matches.
                    _triInvA[t] = _triInvB[t] = _triInvC[t] = _triInvD[t] = double.NaN;
                }
                else
                {
                    double inv = 1.0 / det;
                    _triInvA[t] = d * inv;
                    _triInvB[t] = -b * inv;
                    _triInvC[t] = -c * inv;
                    _triInvD[t] = a * inv;
                }

                double tMinX = Math.Min(p0.X, Math.Min(p1.X, p2.X));
                double tMinY = Math.Min(p0.Y, Math.Min(p1.Y, p2.Y));
                double tMaxX = Math.Max(p0.X, Math.Max(p1.X, p2.X));
                double tMaxY = Math.Max(p0.Y, Math.Max(p1.Y, p2.Y));
                _triMinX[t] = tMinX; _triMinY[t] = tMinY; _triMaxX[t] = tMaxX; _triMaxY[t] = tMaxY;

                if (tMinX < minX) minX = tMinX;
                if (tMinY < minY) minY = tMinY;
                if (tMaxX > maxX) maxX = tMaxX;
                if (tMaxY > maxY) maxY = tMaxY;

                _elementIdToTri[(int)e.Id] = t;
                t++;
            }

            // Trim if some elements were skipped.
            if (t != triCount)
            {
                Array.Resize(ref _triElementIds, t);
                Array.Resize(ref _triPhysicalTags, t);
                Array.Resize(ref _triN0, t); Array.Resize(ref _triN1, t); Array.Resize(ref _triN2, t);
                Array.Resize(ref _triX0, t); Array.Resize(ref _triY0, t);
                Array.Resize(ref _triInvA, t); Array.Resize(ref _triInvB, t);
                Array.Resize(ref _triInvC, t); Array.Resize(ref _triInvD, t);
                Array.Resize(ref _triMinX, t); Array.Resize(ref _triMinY, t);
                Array.Resize(ref _triMaxX, t); Array.Resize(ref _triMaxY, t);
                triCount = t;
            }

            // Build adjacency: triangles sharing two corner nodes are neighbours.
            _neighbors = BuildNeighbors(triCount);

            // Build a uniform grid sized so each cell averages ~1-2 tris.
            if (triCount == 0 || !double.IsFinite(minX))
            {
                _gridMinX = 0; _gridMinY = 0; _cellW = 1; _cellH = 1; _nx = 1; _ny = 1;
                _cellTris = new int[1][] { Array.Empty<int>() };
                return;
            }

            // Pad slightly so points exactly on the max edges still hash inside.
            double pad = 1e-9 * Math.Max(maxX - minX, maxY - minY);
            if (pad <= 0) pad = 1e-12;
            _gridMinX = minX - pad;
            _gridMinY = minY - pad;
            double extentX = (maxX + pad) - _gridMinX;
            double extentY = (maxY + pad) - _gridMinY;

            int target = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(triCount)));
            _nx = Math.Max(1, target);
            _ny = Math.Max(1, target);
            _cellW = extentX / _nx;
            _cellH = extentY / _ny;
            if (_cellW <= 0) _cellW = 1;
            if (_cellH <= 0) _cellH = 1;

            var buckets = new List<int>[_nx * _ny];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();

            for (int i = 0; i < triCount; i++)
            {
                int ix0 = ClampCellX(_triMinX[i]);
                int iy0 = ClampCellY(_triMinY[i]);
                int ix1 = ClampCellX(_triMaxX[i]);
                int iy1 = ClampCellY(_triMaxY[i]);
                for (int iy = iy0; iy <= iy1; iy++)
                    for (int ix = ix0; ix <= ix1; ix++)
                        buckets[iy * _nx + ix].Add(i);
            }

            _cellTris = new int[buckets.Length][];
            for (int i = 0; i < buckets.Length; i++) _cellTris[i] = buckets[i].ToArray();
        }

        /// <summary>
        /// Locate the triangle containing <paramref name="x"/>,<paramref name="y"/>.
        /// </summary>
        /// <param name="hintElementId">
        /// Optional last-known element id. When provided, the locator first tests this
        /// element and its edge neighbours before falling back to the spatial index.
        /// </param>
        public bool TryLocate(double x, double y, int hintElementId, out Hit hit)
        {
            // 1) Walk-locate from the hint.
            if (hintElementId > 0 && _elementIdToTri.TryGetValue(hintElementId, out int hintTri))
            {
                if (TryHit(hintTri, x, y, out hit)) return true;
                for (int k = 0; k < 3; k++)
                {
                    int nb = _neighbors[hintTri, k];
                    if (nb >= 0 && TryHit(nb, x, y, out hit)) return true;
                }
            }

            // 2) Fall back to grid lookup.
            int ix = CellX(x);
            int iy = CellY(y);
            if (ix < 0 || iy < 0 || ix >= _nx || iy >= _ny)
            {
                hit = default;
                return false;
            }

            var bucket = _cellTris[iy * _nx + ix];
            foreach (int ti in bucket)
            {
                if (TryHit(ti, x, y, out hit)) return true;
            }

            hit = default;
            return false;
        }

        /// <summary>Compute barycentric coordinates without a containment test (raw).</summary>
        public bool TryGetBarycentric(int elementId, double x, double y,
            out double b0, out double b1, out double b2)
        {
            if (!_elementIdToTri.TryGetValue(elementId, out int ti))
            {
                b0 = b1 = b2 = 0;
                return false;
            }
            Bary(ti, x, y, out b0, out b1, out b2);
            return true;
        }

        private static bool IsTriangle(int gmshType) => gmshType == 2 || gmshType == 9;

        private bool TryHit(int ti, double x, double y, out Hit hit)
        {
            Bary(ti, x, y, out double b0, out double b1, out double b2);
            // Small tolerance so points on edges of the *correct* triangle still hit.
            const double eps = 1e-9;
            if (b0 >= -eps && b1 >= -eps && b2 >= -eps)
            {
                hit = new Hit(
                    _triElementIds[ti], _triPhysicalTags[ti],
                    _triN0[ti], _triN1[ti], _triN2[ti],
                    b0, b1, b2);
                return true;
            }
            hit = default;
            return false;
        }

        private void Bary(int ti, double x, double y,
            out double b0, out double b1, out double b2)
        {
            double dx = x - _triX0[ti];
            double dy = y - _triY0[ti];
            double l1 = _triInvA[ti] * dx + _triInvB[ti] * dy;
            double l2 = _triInvC[ti] * dx + _triInvD[ti] * dy;
            b1 = l1;
            b2 = l2;
            b0 = 1.0 - l1 - l2;
        }

        private int CellX(double x) => (int)Math.Floor((x - _gridMinX) / _cellW);
        private int CellY(double y) => (int)Math.Floor((y - _gridMinY) / _cellH);
        private int ClampCellX(double x) => Math.Clamp(CellX(x), 0, _nx - 1);
        private int ClampCellY(double y) => Math.Clamp(CellY(y), 0, _ny - 1);

        private int[,] BuildNeighbors(int triCount)
        {
            // Map each undirected edge (sorted node ids) -> first triangle that owns it.
            // When a second triangle hits the same edge, wire them as neighbours.
            var edges = new Dictionary<long, (int Tri, int LocalEdge)>(triCount * 3);
            var nb = new int[triCount, 3];
            for (int i = 0; i < triCount; i++) for (int j = 0; j < 3; j++) nb[i, j] = -1;

            for (int i = 0; i < triCount; i++)
            {
                int a = _triN0[i], b = _triN1[i], c = _triN2[i];
                // Edges, in the same local order as a tracer that walks across edge k:
                //   edge 0 = opposite N0 = (N1, N2)
                //   edge 1 = opposite N1 = (N2, N0)
                //   edge 2 = opposite N2 = (N0, N1)
                ProcessEdge(edges, nb, i, 0, b, c);
                ProcessEdge(edges, nb, i, 1, c, a);
                ProcessEdge(edges, nb, i, 2, a, b);
            }
            return nb;
        }

        private static void ProcessEdge(
            Dictionary<long, (int Tri, int LocalEdge)> edges,
            int[,] nb, int tri, int localEdge, int n1, int n2)
        {
            long key = EdgeKey(n1, n2);
            if (edges.TryGetValue(key, out var prev))
            {
                nb[tri, localEdge] = prev.Tri;
                nb[prev.Tri, prev.LocalEdge] = tri;
            }
            else
            {
                edges[key] = (tri, localEdge);
            }
        }

        private static long EdgeKey(int a, int b)
        {
            int lo = Math.Min(a, b);
            int hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }
    }
}
