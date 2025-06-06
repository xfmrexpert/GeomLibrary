// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class GeomLine : GeomEntity
    {
        public override GeomEntityType Type
        {
            get
            {
                return GeomEntityType.Line; 
            }
        }

        public GeomPoint pt1 { get; set; }
        public GeomPoint pt2 { get; set; }

        public GeomLine(GeomPoint pt1, GeomPoint pt2)
        {
            this.pt1 = pt1;
            this.pt2 = pt2;
        }

        // Check if a point is on this line segment
        public override bool Contains(GeomPoint point)
        {
            double crossProduct = (point.y - pt1.y) * (pt2.x - pt1.x) - (point.x - pt1.x) * (pt2.y - pt1.y);
            if (Math.Abs(crossProduct) > 1e-6) return false;
            double dotProduct = (point.x - pt1.x) * (pt2.x - pt1.x) + (point.y - pt1.y) * (pt2.y - pt1.y);
            if (dotProduct < 0) return false;

            double squaredLength = (pt2.x - pt1.x) * (pt2.x - pt1.x) + (pt2.y - pt1.y) * (pt2.y - pt1.y);
            return dotProduct <= squaredLength;
        }
    }
}
