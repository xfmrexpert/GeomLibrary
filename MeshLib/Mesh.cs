using System;
using System.Collections.Generic;
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

        public List<(MeshVertex, MeshVertex)> GetUniqueEdges()
        {
            var edgeSet = new HashSet<(MeshVertex, MeshVertex)>();

            foreach (var halfEdge in HalfEdges)
            {
                var edge = (halfEdge.PrevVertex, halfEdge.NextVertex);
                if (edge.NextVertex != null && !edgeSet.Contains((edge.NextVertex, edge.PrevVertex)))
                {
                    edgeSet.Add(edge);
                }
            }

            return edgeSet.ToList();
        }

        public void ReadFromMSH2File(string fileName)
        {
            GmshFile gmshFile = GmshFile.Parse(fileName);
            Nodes = new List<MeshNode>(new MeshNode[Nodes.Count + 1]);
            Vertices = new List<MeshVertex>(new MeshVertex[Nodes.Count + 1]);
            foreach (var node in gmshFile.Nodes)
            {
                MeshNode newNode = new MeshNode(node.Id, node.X, node.Y, node.Z);
                Nodes.Insert((int)node.Id, newNode);
                MeshVertex newVertex = new MeshVertex(node.Id, newNode);
                Vertices.Insert((int)node.Id, newVertex);
            }
            List<GmshElement> postProcessElements = new List<GmshElement>();
            foreach (var elem in gmshFile.Elements)
            {
                switch (elem.Type)
                {
                    case 1: // 2-node line
                        // We'll need to locate the edge via the nodes and assign the first tag ID to AttribID
                        // Actually, we can't be sure the edge has been created yet, so I think we'll need to push 
                        // these back onto a list and process in a separate step.
                        postProcessElements.Add(elem);
                        break;
                    case 2:  // 3-node triangle
                        if (elem.Nodes.Count != 3) throw new Exception("Expected three nodes");
                        MeshFace newFace = new MeshFace();
                        MeshHalfEdge edge1 = new MeshHalfEdge();
                        MeshHalfEdge edge2 = new MeshHalfEdge();
                        MeshHalfEdge edge3 = new MeshHalfEdge();
                        MeshVertex vertex1 = Vertices[elem.Nodes[0]];
                        MeshVertex vertex2 = Vertices[elem.Nodes[1]];
                        MeshVertex vertex3 = Vertices[elem.Nodes[2]];
                        edge1.NextVertex = vertex1;
                        edge1.NextHalfEdge = edge2;
                        edge1.PrevHalfEdge = edge3;
                        edge1.Face = newFace;
                        edge2.NextVertex = vertex2;
                        edge2.NextHalfEdge = edge3;
                        edge2.PrevHalfEdge = edge1;
                        edge2.Face = newFace;
                        edge3.NextVertex = vertex3;
                        edge3.NextHalfEdge = edge1;
                        edge3.PrevHalfEdge = edge2;
                        edge3.Face = newFace;
                        HalfEdges.Add(edge1);
                        HalfEdges.Add(edge2);
                        HalfEdges.Add(edge3);
                        newFace.HalfEdge = edge1;
                        if (elem.Tags.Count > 0)
                        {
                            newFace.AttribID = elem.Tags[0];
                        }
                        Faces.Add(newFace);
                        break;
                    case 15: // 1-node point
                        // Find the node, slap on the AttribID
                        // Same as for edges. Need to just push the node onto a list and process after
                        postProcessElements.Add(elem);
                        break;
                    default:
                        throw new NotImplementedException("Unrecognized element type");
                }
            }
            // Fix-up the half edges
            foreach (var halfEdge in HalfEdges)
            {
                if (halfEdge.OppHalfEdge != null)
                {
                    foreach (var halfEdge2 in HalfEdges)
                    {
                        if (halfEdge != halfEdge2 && halfEdge2.OppHalfEdge != null)
                        {
                            if (halfEdge.NextVertex == halfEdge2.PrevVertex && halfEdge.PrevVertex == halfEdge2.NextVertex)
                            {
                                halfEdge.OppHalfEdge = halfEdge2;
                                halfEdge2.OppHalfEdge = halfEdge;
                                break;
                            }
                        }
                    }
                }
            }
            // Process the tags for edges and nodes
            foreach (var elem in postProcessElements)
            {
                if (elem.Tags.Count > 0)
                {
                    if (elem.Type == 1)
                    {
                        uint n1 = (uint)elem.Nodes[0];
                        uint n2 = (uint)elem.Nodes[1];
                        foreach (var halfEdge in HalfEdges)
                        {
                            if (((halfEdge.PrevVertex.ID == n1) && (halfEdge.NextVertex.ID == n2)) || ((halfEdge.PrevVertex.ID == n2) && (halfEdge.NextVertex.ID == n1)))
                            {
                                halfEdge.AttribID = elem.Tags[0];
                                halfEdge.OppHalfEdge.AttribID = elem.Tags[0];
                            }
                        }
                    }
                    else if (elem.Type == 15)
                    {
                        uint n1 = (uint)elem.Nodes[0];
                        foreach (var vertex in Vertices)
                        {
                            if (vertex.ID == n1)
                            {
                                vertex.Node.AttribID = elem.Tags[0];
                            }
                        }
                    }
                    else { throw new Exception("Unexpected element type"); }
                }
            }
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
