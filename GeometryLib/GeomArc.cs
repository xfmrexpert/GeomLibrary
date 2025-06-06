// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GeometryLib
{
    // This is presently configured for a circular arc segment.  An open question is whether to extend to an elliptical arc segment.
    // Note that SweepAngle is assumed to proceed clockwise (which may actually be counter-intuitive)
    // Possible TODO: Allow for start point, center point, end point definition of circular arc
    // TODO: Only need start or end point, not both, if sweep angle is given.
    public class GeomArc : GeomEntity
    {
        public override GeomEntityType Type
        {
            get
            {
                return GeomEntityType.Arc; 
            }
        }


        public GeomPoint StartPt { get; set; }
        public GeomPoint EndPt { get; set; }
        public double SweepAngle { get; private set; } // In radians

        // Calculated center point (read-only)
        public GeomPoint Center
        {
            get
            {
                double chordLength = Math.Sqrt(Math.Pow(EndPt.x - StartPt.x, 2) + Math.Pow(EndPt.y - StartPt.y, 2));
                double radius = chordLength / (2 * Math.Sin(Math.Abs(SweepAngle) / 2));

                double midX = (StartPt.x + EndPt.x) / 2.0;
                double midY = (StartPt.y + EndPt.y) / 2.0;

                double distanceToCenter = Math.Sqrt(radius * radius - (chordLength / 2) * (chordLength / 2));

                double chordVectorX = (EndPt.x - StartPt.x) / chordLength;
                double chordVectorY = (EndPt.y - StartPt.y) / chordLength;

                double perpVectorX = -chordVectorY;
                double perpVectorY = chordVectorX;

                double centerX = midX + (SweepAngle > 0 ? perpVectorX : -perpVectorX) * distanceToCenter;
                double centerY = midY + (SweepAngle > 0 ? perpVectorY : -perpVectorY) * distanceToCenter;

                return new GeomPoint(centerX, centerY);
            }
        }

        // Calculated radius (read-only)
        public double Radius
        {
            get
            {
                double chordLength = Math.Sqrt(Math.Pow(EndPt.x - StartPt.x, 2) + Math.Pow(EndPt.y - StartPt.y, 2));
                return chordLength / (2 * Math.Sin(Math.Abs(SweepAngle) / 2));
            }
        }

        // Constructor: Start point, end point, and sweep angle
        public GeomArc(GeomPoint startPt, GeomPoint endPt, double sweepAngle)
        {
            if (startPt == null || endPt == null)
                throw new ArgumentNullException("StartPoint and EndPoint cannot be null.");
            if (Math.Abs(sweepAngle) <= double.Epsilon)
                throw new ArgumentException("Sweep angle cannot be zero.");

            StartPt = startPt;
            EndPt = endPt;
            SweepAngle = sweepAngle;
        }

        // Contains method: Check if a point lies on the arc
        public bool Contains(GeomPoint point, double tolerance = 1e-6)
        {
            if (point == null)
                throw new ArgumentNullException("Point cannot be null.");

            // Check if the point is on the circle (radius match within tolerance)
            double distanceToCenter = Math.Sqrt(Math.Pow(point.x - Center.x, 2) + Math.Pow(point.y - Center.y, 2));
            if (!distanceToCenter.AboutEquals(Radius))
                return false;

            // Compute the angle of the point relative to the center
            double angleToPoint = Math.Atan2(point.y - Center.y, point.x - Center.x);

            // Normalize angles to [0, 2π)
            double startAngle = NormalizeAngle(Math.Atan2(StartPt.y - Center.y, StartPt.x - Center.x));
            double endAngle = NormalizeAngle(startAngle + SweepAngle);
            angleToPoint = NormalizeAngle(angleToPoint);

            // Check if the angleToPoint lies within the arc's angular range
            if (SweepAngle > 0)
            {
                // Counterclockwise
                return angleToPoint >= startAngle && angleToPoint <= endAngle;
            }
            else
            {
                // Clockwise
                return angleToPoint <= startAngle && angleToPoint >= endAngle;
            }
        }

        // Check if an angle lies within the arc's angular range
        public bool IsAngleInArc(double angle)
        {
            double startAngle = NormalizeAngle(Math.Atan2(StartPt.y - Center.y, StartPt.x - Center.x));
            double endAngle = NormalizeAngle(startAngle + SweepAngle);

            // Handle counterclockwise and clockwise arcs
            if (SweepAngle > 0)
            {
                // Counterclockwise
                return angle >= startAngle && angle <= endAngle;
            }
            else
            {
                // Clockwise
                return angle <= startAngle || angle >= endAngle;
            }
        }

        // Normalize an angle to the range [0, 2π)
        private double NormalizeAngle(double angle)
        {
            while (angle < 0) angle += 2 * Math.PI;
            while (angle >= 2 * Math.PI) angle -= 2 * Math.PI;
            return angle;
        }

        //private GeomPoint[] CalculateCenters()
        //{
        //    // Calculate the midpoint between the start and end points
        //    double midX = (StartPt.x + EndPt.x) / 2.0;
        //    double midY = (StartPt.y + EndPt.y) / 2.0;

        //    // Distance between the start and end points
        //    double distance = Math.Sqrt((EndPt.x - StartPt.x) * (EndPt.x - StartPt.x) + (EndPt.y - StartPt.y) * (EndPt.y - StartPt.y));

        //    // Check if the given radius is valid
        //    if (distance / 2.0 > Radius)
        //    {
        //        throw new ArgumentException("The given radius is too small to form an arc with the provided start and end points.");
        //    }

        //    // Distance from the midpoint to the center of the circle
        //    double offsetDistance = Math.Sqrt(Radius * Radius - (distance / 2.0) * (distance / 2.0));

        //    // Calculate the unit vector perpendicular to the line segment
        //    double dx = (EndPt.x - StartPt.x) / distance;
        //    double dy = (EndPt.y - StartPt.y) / distance;
        //    double perpendicularX = -dy;
        //    double perpendicularY = dx;

        //    // Calculate the two possible centers
        //    GeomPoint center1 = new GeomPoint(midX + perpendicularX * offsetDistance, midY + perpendicularY * offsetDistance);
        //    GeomPoint center2 = new GeomPoint(midX - perpendicularX * offsetDistance, midY - perpendicularY * offsetDistance);

        //    return new GeomPoint[] { center1, center2 };
        //}

        //public override bool Contains(GeomPoint point)
        //{
        //    // Calculate the two possible centers
        //    GeomPoint[] centers = CalculateCenters();
        //    bool Clockwise = false;
        //    if (SweepAngle > 0) Clockwise = true;
        //    GeomPoint center = Clockwise ? centers[0] : centers[1];

        //    // Calculate the radius from the center to the point
        //    double distToCenter = Math.Sqrt((point.x - center.x) * (point.x - center.x) + (point.y - center.y) * (point.y - center.y));

        //    // Check if the point lies on the circle defined by the radius
        //    if (Math.Abs(distToCenter - Radius) > 1e-6)
        //        return false;

        //    // Calculate the angles
        //    double angleStart = Math.Atan2(StartPt.y - center.y, StartPt.x - center.x);
        //    double angleEnd = Math.Atan2(EndPt.y - center.y, EndPt.x - center.x);
        //    double anglePoint = Math.Atan2(point.y - center.y, point.x - center.x);

        //    // Normalize angles
        //    if (angleStart < 0) angleStart += 2 * Math.PI;
        //    if (angleEnd < 0) angleEnd += 2 * Math.PI;
        //    if (anglePoint < 0) anglePoint += 2 * Math.PI;

        //    if (Clockwise)
        //    {
        //        if (angleEnd > angleStart) angleEnd -= 2 * Math.PI;
        //        return anglePoint <= angleStart && anglePoint >= angleEnd;
        //    }
        //    else
        //    {
        //        if (angleEnd < angleStart) angleEnd += 2 * Math.PI;
        //        return anglePoint >= angleStart && anglePoint <= angleEnd;
        //    }
        //}
    }
}
