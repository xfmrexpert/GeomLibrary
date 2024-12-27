using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshLib
{
    public class MeshNode : MeshEntity
    {
        public double X { get; set; }
        public double Y { get; set; }   
        public double Z { get; set; }
        public int AttribID { get; set; }

        public MeshNode() { }

        public MeshNode(uint NodeID, double x, double y, double z, int attribID=0)
        {
            ID = NodeID;
            X = x;
            Y = y;
            Z = z;
            AttribID = attribID;
        }
    }
}
