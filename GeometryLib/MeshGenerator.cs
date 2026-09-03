// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text;
using MathNet.Numerics;
using MeshLib;

namespace GeometryLib
{
    public class MeshGenerator
    {
        private readonly GmshFile gmshFile;
        public bool CaptureOutputOnSuccess { get; set; } = false;
        public bool ShowInTerminal { get; set; } = false;

        /// <summary>
        /// Curvature-based sizing target forwarded to the emitted <c>.geo</c> (see
        /// <see cref="GmshFile.MeshSizeFromCurvature"/>): the number of elements gmsh places
        /// around a full 2π turn. <c>0</c> (default) disables it and leaves the output
        /// unchanged; a positive value resolves true arc geometry (e.g. conductor corner
        /// radii) on the initial mesh.
        /// </summary>
        public int MeshSizeFromCurvature { get; set; } = 80;

        // Configurable paths with smart defaults
        public string? GmshPath { get; set; }

        public MeshGenerator()
        {
            gmshFile = new GmshFile(); // ensure filename set
        }

        public void AddGeometry(Geometry geometry)
        {
            gmshFile.CreateFromGeometry(geometry);
        }

        // ---------------------------------------------------------------------
        // Mesh refinement (option 2: field-based local sizing).
        //
        // Typical usage:
        //     meshGen.AddGeometry(geometry);
        //     meshGen.AddDistanceRefinement(
        //         curves: new[] { conductorCornerArc1, conductorCornerArc2 },
        //         sizeMin: 0.0005, sizeMax: 0.05,
        //         distMin: 0.0,    distMax: 0.01);
        //     meshGen.GenerateMesh("case.geo");
        //
        // Internally each call appends a Distance + Threshold pair and rebuilds
        // the Min combiner used as the background field, so subsequent calls
        // accumulate. The size at any point becomes the minimum size requested
        // by any of the added refinements.
        // ---------------------------------------------------------------------
        private GmshMinField? _refinementCombiner;

        /// <summary>
        /// Add a distance-based local refinement around the given geometric entities.
        /// Inside <paramref name="distMin"/> the target element size is <paramref name="sizeMin"/>;
        /// beyond <paramref name="distMax"/> it relaxes back to <paramref name="sizeMax"/>.
        /// Pass any combination of <see cref="GeomLine"/>, <see cref="GeomArc"/>, and
        /// <see cref="GeomPoint"/> in <paramref name="curves"/> / <paramref name="points"/>.
        /// </summary>
        public void AddDistanceRefinement(
            IEnumerable<object>? curves = null,
            IEnumerable<GeomPoint>? points = null,
            double sizeMin = 0.001,
            double sizeMax = 1.0,
            double distMin = 0.0,
            double distMax = 0.01,
            int sampling = 100,
            bool sigmoid = true)
        {
            var dist = new GmshDistanceField { ID = gmshFile.NewFieldID(), Sampling = sampling };

            if (curves != null)
            {
                foreach (var c in curves)
                {
                    int id = ResolveCurveId(c);
                    if (id > 0) dist.CurvesList.Add(id);
                }
            }
            if (points != null)
            {
                foreach (var p in points)
                {
                    var gp = gmshFile.FindPoint(p);
                    if (gp != null) dist.PointsList.Add(gp.ID);
                }
            }

            if (dist.CurvesList.Count == 0 && dist.PointsList.Count == 0)
                throw new ArgumentException("AddDistanceRefinement: no resolvable curves or points were provided. Call AddGeometry first and pass entities that exist in the geometry.");

            var thr = new GmshThresholdField
            {
                ID = gmshFile.NewFieldID(),
                InField = dist.ID,
                SizeMin = sizeMin,
                SizeMax = sizeMax,
                DistMin = distMin,
                DistMax = distMax,
                Sigmoid = sigmoid,
            };

            gmshFile.fields.Add(dist);
            gmshFile.fields.Add(thr);

            if (_refinementCombiner == null)
            {
                _refinementCombiner = new GmshMinField { ID = gmshFile.NewFieldID() };
                gmshFile.fields.Add(_refinementCombiner);
                gmshFile.BackgroundFieldId = _refinementCombiner.ID;
            }
            _refinementCombiner.FieldsList.Add(thr.ID);
        }

        private int ResolveCurveId(object curve)
        {
            switch (curve)
            {
                case GeomLine gl:
                    var l = gmshFile.FindLine(gl);
                    return l?.ID ?? 0;
                case GeomArc ga:
                    var a = gmshFile.FindArc(ga);
                    return a?.ID ?? 0;
                default:
                    throw new ArgumentException(
                        $"AddDistanceRefinement: unsupported curve type '{curve?.GetType().Name ?? "null"}'. Expected GeomLine or GeomArc.");
            }
        }

        private static readonly bool IsWindows = OperatingSystem.IsWindows();
        private static readonly string[] GmshNames = IsWindows ? ["gmsh.exe", "gmsh"] : ["gmsh"];

        private string FindGmshExecutable()
        {
            // 1. Explicit configuration (highest priority)
            if (!string.IsNullOrEmpty(GmshPath) && File.Exists(GmshPath))
                return GmshPath;

            // 2. Environment variable
            var envPath = Environment.GetEnvironmentVariable("GMSH_PATH");
            envPath = string.IsNullOrEmpty(envPath) ? null : envPath.Trim('"');
            if (!string.IsNullOrEmpty(envPath) & File.Exists(envPath))
            {
                return envPath;
            }

            // 3. Relative to current executable/working directory
            string[] relativeDirs = {
                "./bin",           // same folder as exe
                "../bin",          // parent/bin
                "../../../bin",    // your current setup
                "."
            };

            foreach (var dir in relativeDirs)
            {
                foreach (var name in GmshNames)
                {
                    var rel = Path.Combine(dir, name);
                    if (File.Exists(rel)) return Path.GetFullPath(rel);
                }
            }

            // 4. System PATH search
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    foreach (var name in GmshNames)
                    {
                        var candidate = Path.Combine(dir, name);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }

            // 5. Common system locations
            if (IsWindows)
            {
                // Check common Windows install locations
                var programFiles = new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };
                foreach (var pf in programFiles)
                {
                    if (string.IsNullOrEmpty(pf)) continue;
                    var gmshDir = Path.Combine(pf, "gmsh");
                    if (Directory.Exists(gmshDir))
                    {
                        foreach (var name in GmshNames)
                        {
                            var candidate = Path.Combine(gmshDir, name);
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                }
            }
            else
            {
                string[] systemPaths = { "/usr/bin/gmsh", "/usr/local/bin/gmsh", "/opt/gmsh/bin/gmsh" };
                foreach (var sys in systemPaths)
                    if (File.Exists(sys)) return sys;
            }

            throw new FileNotFoundException(
                "gmsh executable not found. Set GmshPath property, GMSH_PATH environment variable, " +
                "or ensure gmsh is in PATH or relative bin/ folder.");
        }

        private string? FindTerminal()
        {
            string[] terminals = { "cosmic-term","gnome-terminal", "xterm", "konsole" };
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    foreach (var term in terminals)
                    {
                        var candidate = Path.Combine(dir, term);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Raised for every line gmsh (and this generator) would otherwise write to the
        /// console. Hosts with their own output surface (e.g. a TUI status display) can
        /// subscribe to keep the console clean; when nothing is subscribed the lines still
        /// go to <see cref="Console"/> so existing callers behave as before.
        /// </summary>
        public event Action<string>? OutputReceived;

        private void ReportOutput(string message)
        {
            if (OutputReceived is { } handler)
                handler(message);
            else
                Console.WriteLine(message);
        }

        public Mesh GenerateMesh(string filename, double meshscale = 1.0, int meshorder = 1)
        {
            string gmshPath = FindGmshExecutable();
            ReportOutput($"Using gmsh at: {gmshPath}");

            gmshFile.MeshSizeFromCurvature = MeshSizeFromCurvature;
            gmshFile.WriteFile(filename);

            // -setnumber Mesh.RemoveDuplicateNodes/Elements 1 collapses coincident nodes/elements
            // that occur along clip-boundary seams between independently-built surfaces. Without
            // this, MFEM's Mesh::Finalize aborts with
            //   "Verification failed: (faces_info[i].Elem2No < 0 || faces_info[i].Elem2Inf%2 != 0)
            //    --> Invalid mesh topology. Interior face with incompatible orientations."
            // because the duplicates leave an interior edge shared by two same-orientation triangles.
            // -setnumber Mesh.RenumberNodes/Elements 1 produces contiguous IDs after the removals so
            // the resulting msh2 file is self-consistent.
            string gmshArgs =
                $"{filename} -2 -order {meshorder} -clscale {meshscale} -format msh2 -v 3" +
                "-setnumber Mesh.RemoveDuplicateNodes 1 " +
                "-setnumber Mesh.RemoveDuplicateElements 1 " +
                "-setnumber Mesh.RenumberNodes 1 " +
                "-setnumber Mesh.RenumberElements 1";

            StringBuilder? sb = CaptureOutputOnSuccess ? new StringBuilder() : null;
            using var p = new Process();

            if (ShowInTerminal)
            {
                var term = FindTerminal() ?? throw new Exception("No terminal emulator (gnome-terminal/xterm/konsole) found.");
                if (term.Contains("gnome-terminal"))
                {
                    p.StartInfo.FileName = term;
                    // --wait makes gnome-terminal exit when the command finishes
                    p.StartInfo.Arguments = $"--wait -- bash -lc \"{gmshPath} {gmshArgs}\"";
                }
                else if (term.Contains("xterm"))
                {
                    p.StartInfo.FileName = term;
                    p.StartInfo.Arguments = $"-e sh -c '{gmshPath} {gmshArgs}'";
                }
                else // konsole
                {
                    p.StartInfo.FileName = term;
                    p.StartInfo.Arguments = $"-e {gmshPath} {gmshArgs}";
                }
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = false;
                p.StartInfo.RedirectStandardError = false;
                p.StartInfo.CreateNoWindow = false;
            }
            else
            {
                p.StartInfo.FileName = gmshPath;
                p.StartInfo.Arguments = gmshArgs;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;

                p.OutputDataReceived += (s, a) =>
                {
                    if (a.Data != null && sb != null)
                    {
                        sb.AppendLine(a.Data);
                        ReportOutput(a.Data);
                    }
                };
                p.ErrorDataReceived += (s, a) =>
                {
                    if (a.Data != null)
                    {
                        sb ??= new StringBuilder();
                        sb.AppendLine(a.Data);
                        ReportOutput(a.Data);
                    }
                };
            }

            p.Start();
            if (!ShowInTerminal)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            const int timeoutMs = 120000;
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException($"gmsh timeout after {timeoutMs} ms");
            }
            // second wait to flush async handlers (non-terminal mode)
            if (!ShowInTerminal) p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception("Failed to run gmsh:\n" + sb?.ToString());

            var mshPath = filename[..^3] + "msh";
            int retries = 0;
            while (!File.Exists(mshPath) && retries++ < 50)
                Thread.Sleep(10);

            var mesh = new Mesh();
            mesh.ReadFromMSH2File(mshPath);
            return mesh;
        }
    }
}