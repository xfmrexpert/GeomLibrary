using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshLib
{
    public class MeshVertex : MeshEntity
    {
        public MeshNode Node { get; set; }
        public MeshHalfEdge HalfEdge { get; set; }

        public MeshVertex(uint vertexID, MeshNode node)
        {
            ID = vertexID;
            Node = node;
            HalfEdge = null;
        }

        public MeshVertex(uint vertexID, MeshNode node, MeshHalfEdge halfEdge)
        {
            ID = vertexID;
            Node = node;
            HalfEdge = halfEdge;
        }

    }
}
