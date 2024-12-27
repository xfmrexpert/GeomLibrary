using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if (edge.Item2 != null && !edgeSet.Contains((edge.Item2, edge.Item1)))
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
            foreach (var elem in gmshFile.Elements)
            {
                switch (elem.Type)
                {
                    case 1:
                        break;
                    case 2: 
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
                        Faces.Add(newFace);
                        break;
                    default:
                        throw new NotImplementedException("Unrecognized element type");
                }
            }
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
        }

    }
}
