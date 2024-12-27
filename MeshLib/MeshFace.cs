using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshLib
{
    public class MeshFace : MeshEntity
    {
        static private uint nextID = 0;

        public MeshHalfEdge HalfEdge { get; set; }

        public MeshFace()
        {
            ID = nextID;
            nextID++;
        }

        public MeshFace(uint faceID)
        {
            ID = faceID;
        }
    }
}
