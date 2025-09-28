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

        private string FindGmshExecutable()
        {
            // 1. Explicit configuration (highest priority)
            if (!string.IsNullOrEmpty(GmshPath) && File.Exists(GmshPath))
                return GmshPath;

            // 2. Environment variable
            var envPath = Environment.GetEnvironmentVariable("GMSH_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
                return envPath;

            // 3. Relative to current executable/working directory
            string[] relativePaths = {
                "./bin/gmsh",           // same folder as exe
                "../bin/gmsh",          // parent/bin
                "../../../bin/gmsh",    // your current setup
                "./gmsh"
            };

            foreach (var rel in relativePaths)
            {
                if (File.Exists(rel)) return Path.GetFullPath(rel);
            }

            // 4. System PATH search
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var candidate = Path.Combine(dir, "gmsh");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            // 5. Common system locations (Linux/Unix)
            string[] systemPaths = { "/usr/bin/gmsh", "/usr/local/bin/gmsh", "/opt/gmsh/bin/gmsh" };
            foreach (var sys in systemPaths)
                if (File.Exists(sys)) return sys;

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

        public Mesh GenerateMesh(string filename, double meshscale = 1.0, int meshorder = 1)
        {
            string gmshPath = FindGmshExecutable();
            Console.WriteLine($"Using gmsh at: {gmshPath}");

            gmshFile.WriteFile(filename);

            string gmshArgs = $"{filename} -2 -order {meshorder} -clscale {meshscale} -format msh2 -v 3";

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
                        Console.WriteLine(a.Data);
                    }
                };
                p.ErrorDataReceived += (s, a) =>
                {
                    if (a.Data != null)
                    {
                        sb ??= new StringBuilder();
                        sb.AppendLine(a.Data);
                        Console.WriteLine(a.Data);
                    }
                };
            }

            p.Start();
            if (!ShowInTerminal)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            const int timeoutMs = 12000000;
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