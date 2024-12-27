using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshLib
{
    public class MeshElement
    {
        public List<int> Nodes { get; set; } = new List<int>();

        public MeshElement() { }

        public MeshElement(List<int> Nodes) { this.Nodes = Nodes; }

    }
}
