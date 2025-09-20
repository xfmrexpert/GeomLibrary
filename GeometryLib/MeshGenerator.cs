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
        public bool ShowInTerminal { get; set; } = true;   // NEW

        public MeshGenerator()
        {
            gmshFile = new GmshFile(); // ensure filename set
        }

        public void AddGeometry(Geometry geometry)
        {
            gmshFile.CreateFromGeometry(geometry);
        }

        private static string? FindTerminal()
        {
            string[] candidates = { "gnome-terminal", "xterm", "konsole" };
            foreach (var c in candidates)
            {
                var full = $"/usr/bin/{c}";
                if (File.Exists(full)) return full;
                if (Environment.GetEnvironmentVariable("PATH")?.Split(':').Any(p => File.Exists(Path.Combine(p, c))) == true)
                    return c;
            }
            return null;
        }

        public Mesh GenerateMesh(double meshscale = 1.0, int meshorder = 1)
        {
            string gmshPath = "./bin/gmsh";
            string filename = "tempgeo.geo";
            Debug.WriteLine($"Current Path: {Environment.CurrentDirectory}");
            if (!File.Exists(gmshPath))
                throw new Exception("Cannot find gmsh executable at " + gmshPath);

            gmshFile.WriteFile(filename);

            string gmshArgs = $"{filename} -2 -order {meshorder} -clscale {meshscale} -format msh2 -v 4";

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