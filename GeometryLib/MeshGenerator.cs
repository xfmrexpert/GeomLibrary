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

        public MeshGenerator()
        {
            gmshFile = new GmshFile(); // ensure filename set
        }

        public void AddGeometry(Geometry geometry)
        {
            gmshFile.CreateFromGeometry(geometry);
        }

        public Mesh GenerateMesh(double meshscale = 1.0, int meshorder = 1)
        {
            string gmshPath = "./bin/gmsh";
            string filename = "tempgeo.geo";
            Debug.WriteLine($"Current Path: {Environment.CurrentDirectory}");
            if (!File.Exists(gmshPath))
                throw new Exception("Cannot find gmsh executable at " + gmshPath);

            gmshFile.WriteFile(filename); 

            StringBuilder? sb = CaptureOutputOnSuccess ? new StringBuilder() : null;
            using var p = new Process();
            p.StartInfo.FileName = gmshPath;
            p.StartInfo.Arguments = $"{filename} -2 -order {meshorder} -clscale {meshscale} -v 2";
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

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception("Failed to run gmsh:\n" + sb?.ToString());

            var mesh = new Mesh();
            mesh.ReadFromMSH2File(filename[..^3] + "msh");
            return mesh;
        }
    }
}