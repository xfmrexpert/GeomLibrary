using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MeshLib
{
    public class GmshFile
    {
        public double Version { get; set; }
        public int FileType { get; set; }
        public int DataSize { get; set; }
        public List<GmshNode> Nodes { get; } = new();
        public List<GmshElement> Elements { get; } = new();
        public List<GmshNodeData> NodeData { get; } = new();

        public static GmshFile Parse(string filePath)
        {
            var mshFile = new GmshFile();
            using var reader = new StreamReader(filePath);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                switch (line)
                {
                    case "$MeshFormat":
                        ParseMeshFormat(reader, mshFile);
                        break;
                    case "$Nodes":
                        ParseNodes(reader, mshFile);
                        break;
                    case "$Elements":
                        ParseElements(reader, mshFile);
                        break;
                    case "$NodeData":
                        ParseNodeData(reader, mshFile);
                        break;
                    default:
                        break;
                }
            }

            return mshFile;
        }

        private static void ParseMeshFormat(StreamReader reader, GmshFile mshFile)
        {
            var formatLine = reader.ReadLine()?.Trim().Split(' ');
            if (formatLine == null || formatLine.Length < 3)
                throw new InvalidDataException("Invalid $MeshFormat section.");

            mshFile.Version = double.Parse(formatLine[0], CultureInfo.InvariantCulture);
            mshFile.FileType = int.Parse(formatLine[1]);
            mshFile.DataSize = int.Parse(formatLine[2]);

            // Skip the $EndMeshFormat line
            reader.ReadLine();
        }

        private static void ParseNodes(StreamReader reader, GmshFile mshFile)
        {
            int numberOfNodes = int.Parse(reader.ReadLine()?.Trim() ?? "0");
            for (int i = 0; i < numberOfNodes; i++)
            {
                var nodeLine = reader.ReadLine()?.Trim().Split(' ');
                if (nodeLine == null || nodeLine.Length < 4)
                    throw new InvalidDataException("Invalid node data.");

                uint nodeId = uint.Parse(nodeLine[0]);
                double x = double.Parse(nodeLine[1], CultureInfo.InvariantCulture);
                double y = double.Parse(nodeLine[2], CultureInfo.InvariantCulture);
                double z = double.Parse(nodeLine[3], CultureInfo.InvariantCulture);

                mshFile.Nodes.Add(new GmshNode(nodeId, x, y, z));
            }

            // Skip the $EndNodes line
            reader.ReadLine();
        }

        private static void ParseElements(StreamReader reader, GmshFile mshFile)
        {
            int numberOfElements = int.Parse(reader.ReadLine()?.Trim() ?? "0");
            for (int i = 0; i < numberOfElements; i++)
            {
                var elementLine = reader.ReadLine()?.Trim().Split(' ');
                if (elementLine == null || elementLine.Length < 4)
                    throw new InvalidDataException("Invalid element data.");

                uint elementId = uint.Parse(elementLine[0]);
                int elementType = int.Parse(elementLine[1]);
                int numberOfTags = int.Parse(elementLine[2]);

                var tags = new List<int>();
                for (int t = 0; t < numberOfTags; t++)
                {
                    tags.Add(int.Parse(elementLine[3 + t]));
                }

                var nodeList = new List<int>();
                for (int n = 3 + numberOfTags; n < elementLine.Length; n++)
                {
                    nodeList.Add(int.Parse(elementLine[n]));
                }

                mshFile.Elements.Add(new GmshElement(elementId, elementType, tags, nodeList));
            }

            // Skip the $EndElements line
            reader.ReadLine();
        }

        private static void ParseNodeData(StreamReader reader, GmshFile mshFile)
        {
            int numberOfStringTags = int.Parse(reader.ReadLine()?.Trim() ?? "0");
            var stringTags = new List<string>();
            for (int i = 0; i < numberOfStringTags; i++)
            {
                stringTags.Add(reader.ReadLine()?.Trim('"') ?? string.Empty);
            }

            int numberOfRealTags = int.Parse(reader.ReadLine()?.Trim() ?? "0");
            var realTags = new List<double>();
            for (int i = 0; i < numberOfRealTags; i++)
            {
                realTags.Add(double.Parse(reader.ReadLine()?.Trim() ?? "0", CultureInfo.InvariantCulture));
            }

            int numberOfIntegerTags = int.Parse(reader.ReadLine()?.Trim() ?? "0");
            var integerTags = new List<int>();
            for (int i = 0; i < numberOfIntegerTags; i++)
            {
                integerTags.Add(int.Parse(reader.ReadLine()?.Trim() ?? "0"));
            }

            var nodeDataValues = new List<(int NodeId, double Value)>();
            for (int i = 0; i < integerTags[2]; i++)
            {
                var dataLine = reader.ReadLine()?.Trim().Split(' ');
                if (dataLine == null || dataLine.Length < 2)
                    throw new InvalidDataException("Invalid node data values.");

                int nodeId = int.Parse(dataLine[0]);
                double value = double.Parse(dataLine[1], CultureInfo.InvariantCulture);

                nodeDataValues.Add((nodeId, value));
            }

            mshFile.NodeData.Add(new GmshNodeData(stringTags, realTags, integerTags, nodeDataValues));

            // Skip the $EndNodeData line
            reader.ReadLine();
        }
    }

    public record GmshNode(uint Id, double X, double Y, double Z);

    public record GmshElement(uint Id, int Type, List<int> Tags, List<int> Nodes);

    public record GmshNodeData(
        List<string> StringTags,
        List<double> RealTags,
        List<int> IntegerTags,
        List<(int NodeId, double Value)> Data);
}

