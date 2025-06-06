using GeometryLib;

public class Netgen2DFileWriter
{
    private readonly Geometry _geometry;

    public Netgen2DFileWriter(Geometry geometry)
    {
        _geometry = geometry;
    }

    public void Write(string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("geometry2d\n");

        // Emit points
        foreach (var pt in _geometry.Points.OrderBy(p => p.Id))
        {
            writer.WriteLine($"point {pt.Id} {pt.x} {pt.y}");
        }

        // Emit lines
        foreach (var line in _geometry.Lines.OrderBy(l => l.Id))
        {
            writer.WriteLine($"line {line.Id} {line.pt1.Id} {line.pt2.Id}");
        }

        // Emit arcs
        foreach (var arc in _geometry.Arcs.OrderBy(a => a.Id))
        {
            var center = arc.Center;
            if (center == null)
            {
                throw new InvalidOperationException($"Arc {arc.Id} is missing a center point.");
            }
            writer.WriteLine($"circle {arc.Id} {arc.StartPt.Id} {center.Id} {arc.EndPt.Id}");
        }

        // Emit line loops
        foreach (var loop in _geometry.LineLoops.OrderBy(l => l.Id))
        {
            string loopElements = string.Join(" ",
                loop.Boundary.Select(e => e.Id * (loop.Boundary.First(b => b.Id == e.Id).Id > 0 ? 1 : -1)));
            writer.WriteLine($"line loop {loop.Id} {loopElements}");
        }

        // Emit surfaces
        foreach (var surface in _geometry.Surfaces.OrderBy(s => s.Id))
        {
            var holeIds = surface.Holes.Where(h => h != null).Select(h => h.Id);
            string loops = string.Join(" ", new[] { surface.Boundary.Id }.Concat(holeIds));
            writer.WriteLine($"surface {surface.Id} {loops}");
        }
    }
}
