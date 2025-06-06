using GeometryLib;

namespace GeometryLibTests
{
    [TestClass]
    public sealed class GeomArcTests
    {
        [TestMethod]
        public void GetBoundingBox_SimpleArc_ReturnsCorrectBoundingBox()
        {
            // Arrange
            GeomPoint startPoint = new GeomPoint(0, 1);
            GeomPoint endPoint = new GeomPoint(0, -1);
            double sweepAngle = -Math.PI; // 180 degrees counterclockwise
            GeomLine line = new GeomLine(startPoint, endPoint);
            GeomArc arc = new GeomArc(startPoint, endPoint, sweepAngle);
            GeomLineLoop loop = new GeomLineLoop(new List<GeomEntity>{line, arc});
            // Act
            (double minX, double maxX, double minY, double maxY) = loop.GetBoundingBox();

            // Assert
            Assert.AreEqual(0, minX, 1e-6, "Min X should be 0");
            Assert.AreEqual(-1, minY, 1e-6, "Min Y should be -1");
            Assert.AreEqual(1, maxX, 1e-6, "Max X should be 1");
            Assert.AreEqual(1, maxY, 1e-6, "Max Y should be 1");
        }

        [TestMethod]
        public void GetBoundingBox_SmallSweepAngle_ReturnsTightBoundingBox()
        {
            // Arrange
            //Point2D startPoint = new Point2D(1, 0);
            //Point2D endPoint = new Point2D(Math.Sqrt(2) / 2, Math.Sqrt(2) / 2); // ~45 degrees
            //double sweepAngle = Math.PI / 4; // 45 degrees counterclockwise

            //GeomArc arc = new GeomArc(startPoint, endPoint, sweepAngle);

            //// Act
            //BoundingBox bbox = arc.GetBoundingBox();

            //// Assert
            //Assert.AreEqual(Math.Sqrt(2) / 2, bbox.MinX, 1e-6, "Min X ~ 0.707");
            //Assert.AreEqual(0, bbox.MinY, 1e-6, "Min Y = 0");
            //Assert.AreEqual(1, bbox.MaxX, 1e-6, "Max X = 1");
            //Assert.AreEqual(Math.Sqrt(2) / 2, bbox.MaxY, 1e-6, "Max Y ~ 0.707");
        }

        [TestMethod]
        public void GetBoundingBox_FullCircle_ReturnsCorrectBoundingBox()
        {
            // Arrange
            //Point2D startPoint = new Point2D(1, 0);
            //Point2D endPoint = new Point2D(1, 0); // Full circle
            //double sweepAngle = 2 * Math.PI;

            //GeomArc arc = new GeomArc(startPoint, endPoint, sweepAngle);

            //// Act
            //BoundingBox bbox = arc.GetBoundingBox();

            //// Assert
            //Assert.AreEqual(-1, bbox.MinX, 1e-6, "Min X should be -1");
            //Assert.AreEqual(-1, bbox.MinY, 1e-6, "Min Y should be -1");
            //Assert.AreEqual(1, bbox.MaxX, 1e-6, "Max X should be 1");
            //Assert.AreEqual(1, bbox.MaxY, 1e-6, "Max Y should be 1");
        }
    }
}
