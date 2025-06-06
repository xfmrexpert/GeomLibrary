using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public abstract class GeometryWriter
    {
        protected readonly Geometry geometry;

        protected GeometryWriter(Geometry _geometry)
        {
            geometry = _geometry;
        }

        public abstract void Write(string filePath);
    }
}
