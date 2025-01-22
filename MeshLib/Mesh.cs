using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MeshLib
{
    public class Mesh
    {
        public List<MeshNode> Nodes { get; set; } = new();
        public List<MeshVertex> Vertices { get; set; } = new();
        public List<MeshHalfEdge> HalfEdges { get; set; } = new();
        public List<MeshFace> Faces { get; set; } = new();

        private Dictionary<(int, int), MeshHalfEdge> edgeDict = new();

        public List<(MeshVertex, MeshVertex)> GetUniqueEdges()
        {
            var edgeSet = new HashSet<(MeshVertex, MeshVertex)>();
            foreach (var he in HalfEdges)
            {
                var vStart = he.PrevVertex;
                var vEnd = he.NextVertex;
                // Add an ordered pair so that we ignore duplicates
                if (vStart == null || vEnd == null) continue;
                var forward = (vStart, vEnd);
                var backward = (vEnd, vStart);
                if (!edgeSet.Contains(forward) && !edgeSet.Contains(backward))
                {
                    edgeSet.Add(forward);
                }
            }
            return edgeSet.ToList();
        }


        public void ReadFromMSH2File(string fileName, bool createBoundaryEdges = true)
        {
            var gmshFile = GmshFile.Parse(fileName);

            // 1) Prepare node/vertex arrays sized by max node ID
            uint maxNodeID = gmshFile.Nodes.Max(n => n.Id);
            Nodes = new List<MeshNode>(new MeshNode[(int)maxNodeID + 1]);
            Vertices = new List<MeshVertex>(new MeshVertex[(int)maxNodeID + 1]);

            // 2) Insert nodes & vertices
            foreach (var node in gmshFile.Nodes)
            {
                var newNode = new MeshNode(node.Id, node.X, node.Y, node.Z);
                var newVert = new MeshVertex(node.Id, newNode);

                Nodes[(int)node.Id] = newNode;
                Vertices[(int)node.Id] = newVert;
            }

            // We'll store line (type=1) or point (type=15) elements for later
            var postProcessElements = new List<GmshElement>();

            // 3) Build faces + half-edges for each triangle
            foreach (var elem in gmshFile.Elements)
            {
                switch (elem.Type)
                {
                    case 2: // 3-node triangle
                        MakeTriangle(elem);
                        break;

                    case 1: // 2-node line
                    case 15: // 1-node point
                        postProcessElements.Add(elem);
                        break;

                    default:
                        throw new NotImplementedException($"Element type {elem.Type} not handled.");
                }
            }

            // 4) Optional: create boundary half‐edges if no opposite found
            if (createBoundaryEdges)
                CreateBoundaryEdges();

            // 5) Post-process lines & points (assigning attributes)
            foreach (var elem in postProcessElements)
            {
                if (elem.Tags.Count == 0) continue;

                if (elem.Type == 1 && elem.Nodes.Count == 2) // line => set Attrib on half-edge
                {
                    int v1 = elem.Nodes[0];
                    int v2 = elem.Nodes[1];
                    var he = GetEdgeByVertices(v1, v2);
                    if (he != null)
                    {
                        he.AttribID = elem.Tags[0];
                        if (he.OppHalfEdge != null)
                            he.OppHalfEdge.AttribID = elem.Tags[0];
                    }
                }
                else if (elem.Type == 15 && elem.Nodes.Count == 1) // point => set Attrib on node
                {
                    int v = elem.Nodes[0];
                    if (v >= 0 && v < Nodes.Count && Nodes[v] != null)
                    {
                        Nodes[v].AttribID = elem.Tags[0];
                    }
                }
            }
        }

        /// <summary>
        /// Creates 3 half-edges for a triangle element, linking them
        /// and also linking each to its opposite if found in the dictionary.
        /// </summary>
        private void MakeTriangle(GmshElement elem)
        {
            if (elem.Nodes.Count != 3)
                throw new Exception("Triangle must have exactly 3 node IDs.");

            // Create one Face
            var face = new MeshFace();
            if (elem.Tags.Count > 0)
                face.AttribID = elem.Tags[0];
            Faces.Add(face);

            // Pull out the 3 vertices (assume they exist)
            var v1 = Vertices[elem.Nodes[0]];
            var v2 = Vertices[elem.Nodes[1]];
            var v3 = Vertices[elem.Nodes[2]];

            // Create 3 half-edges
            var e1 = new MeshHalfEdge() { Face = face };
            var e2 = new MeshHalfEdge() { Face = face };
            var e3 = new MeshHalfEdge() { Face = face };

            // "next vertex" for each edge
            // e1: v1->v2, e2: v2->v3, e3: v3->v1
            e1.NextVertex = v2;
            e2.NextVertex = v3;
            e3.NextVertex = v1;

            // link them in a cycle
            e1.NextHalfEdge = e2; e2.NextHalfEdge = e3; e3.NextHalfEdge = e1;
            e1.PrevHalfEdge = e3; e2.PrevHalfEdge = e1; e3.PrevHalfEdge = e2;

            // assign a half-edge to each vertex if none is assigned
            if (v1.HalfEdge == null) v1.HalfEdge = e1;
            if (v2.HalfEdge == null) v2.HalfEdge = e2;
            if (v3.HalfEdge == null) v3.HalfEdge = e3;

            // face points to e1 as a representative
            face.HalfEdge = e1;

            // Add them to the global list
            HalfEdges.Add(e1);
            HalfEdges.Add(e2);
            HalfEdges.Add(e3);

            // Immediately try to link each half-edge's opposite in the dictionary
            LinkOpposite(e1, (int)v1.ID, (int)v2.ID);
            LinkOpposite(e2, (int)v2.ID, (int)v3.ID);
            LinkOpposite(e3, (int)v3.ID, (int)v1.ID);
        }

        /// <summary>
        /// Attempts to find the opposite half-edge for (start->end).
        /// If found, links them. Otherwise, stores the new half-edge in the dictionary.
        /// </summary>
        private void LinkOpposite(MeshHalfEdge he, int startID, int endID)
        {
            var reverseKey = (endID, startID);

            if (edgeDict.TryGetValue(reverseKey, out MeshHalfEdge twin))
            {
                // twin found => link
                he.OppHalfEdge = twin;
                twin.OppHalfEdge = he;
                // remove the twin from the dictionary if we don't need it anymore
                // (Optionally keep it if you want to look up edges by ID pair)
                edgeDict.Remove(reverseKey);
            }
            else
            {
                // no twin => store this one
                var forwardKey = (startID, endID);
                // we only store it if it doesn't already exist in the dictionary
                if (!edgeDict.ContainsKey(forwardKey))
                    edgeDict[forwardKey] = he;
            }
        }

        /// <summary>
        /// In a final pass, any half-edge that lacks an OppHalfEdge is boundary.
        /// We optionally create a boundary half-edge for it (which can be self-loop).
        /// </summary>
        private void CreateBoundaryEdges()
        {
            // We'll gather newly created boundary edges here:
            var newEdges = new List<MeshHalfEdge>();

            // We can safely read HalfEdges in a foreach,
            // as long as we only add to newEdges, not HalfEdges itself.
            foreach (var he in HalfEdges)
            {
                if (he.OppHalfEdge == null)
                {
                    var boundaryHE = new MeshHalfEdge();
                    boundaryHE.NextVertex = he.PrevVertex;
                    boundaryHE.Face = null;
                    boundaryHE.OppHalfEdge = he;
                    he.OppHalfEdge = boundaryHE;

                    // Self-loop to avoid null references
                    boundaryHE.NextHalfEdge = boundaryHE;
                    boundaryHE.PrevHalfEdge = boundaryHE;

                    newEdges.Add(boundaryHE);
                }
            }

            // Now safely append them to the main list AFTER the loop
            HalfEdges.AddRange(newEdges);

            // Clear edgeDict if you wish
            edgeDict.Clear();
        }

        /// <summary>
        /// Example method to find an edge between two vertex IDs by ring-walking.
        /// </summary>
        public MeshHalfEdge GetEdgeByVertices(int vID1, int vID2)
        {
            if (vID1 < 0 || vID1 >= Vertices.Count) return null;
            if (vID2 < 0 || vID2 >= Vertices.Count) return null;
            var v1 = Vertices[vID1];
            var v2 = Vertices[vID2];
            if (v1 == null || v2 == null) return null;

            var startHe = v1.HalfEdge;
            if (startHe == null) return null;

            // ring walk
            MeshHalfEdge he = startHe;
            do
            {
                // check if he goes (v1->v2) or (v2->v1)
                if ((he.PrevVertex == v1 && he.NextVertex == v2) ||
                    (he.PrevVertex == v2 && he.NextVertex == v1))
                {
                    return he;
                }
                // go around: we move backward along prevEdge, then cross OppHalfEdge
                he = he.PrevHalfEdge.OppHalfEdge;
            }
            while (he != startHe);

            return null;
        }

        public void WriteToTriangleFiles(string path, string file_root)
        {
            throw new NotImplementedException();
            // Write .node file
            string nodeFilePath = Path.Combine(path, $"{file_root}.node");
            using (StreamWriter writer = new StreamWriter(nodeFilePath))
            {
                writer.WriteLine($"{Nodes.Count} 2 0 1"); // NumNodes, Dimension, Attributes, BoundaryMarkerCount
                foreach (var node in Nodes)
                {
                    writer.WriteLine($"{node.ID} {node.X} {node.Y} {node.AttribID}");
                }
            }

            // Write .ele file
            string eleFilePath = Path.Combine(path, $"{file_root}.ele");
            using (StreamWriter writer = new StreamWriter(eleFilePath))
            {
                writer.WriteLine($"{Faces.Count} 3 1"); // NumElements, NodesPerElement, Attributes
                foreach (var face in Faces)
                {
                    MeshNode n1 = face.HalfEdge.NextVertex.Node;
                    MeshNode n2 = face.HalfEdge.NextHalfEdge.NextVertex.Node;
                    MeshNode n3 = face.HalfEdge.NextHalfEdge.NextHalfEdge.NextVertex.Node;
                    writer.WriteLine($"{face.ID} {n1.ID} {n2.ID} {n3.ID} {face.AttribID}");
                }
            }

        }

        public void ReadFromTriangleFiles(string path) {
            throw new NotImplementedException(); 
        }

    }
}
