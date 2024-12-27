using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MeshLib
{
    public class MeshHalfEdge : MeshEntity
    {
        static private uint nextID = 0;

        public MeshVertex NextVertex {  get; set; }
        public MeshFace Face { get; set; }
        public MeshHalfEdge NextHalfEdge { get; set; }
        public MeshHalfEdge OppHalfEdge { get; set; }
        public MeshHalfEdge PrevHalfEdge { get; set; }

        public MeshVertex PrevVertex
        {
            get
            {
                return PrevHalfEdge.NextVertex;
            }
        }

        public MeshHalfEdge()
        {
            ID = nextID;
            nextID++;
        }

        public MeshHalfEdge(uint halfEdgeID)
        {
            ID = halfEdgeID;
        }

        public MeshHalfEdge(uint halfEdgeID, MeshVertex nextVertex, MeshFace face, MeshHalfEdge nextHalfEdge, MeshHalfEdge oppHalfEdge, MeshHalfEdge prevHalfEdge)
        {
            ID = halfEdgeID;
            NextVertex = nextVertex;
            Face = face; 
            NextHalfEdge = nextHalfEdge;
            OppHalfEdge = oppHalfEdge;
            PrevHalfEdge = prevHalfEdge;
        }
    }
}
