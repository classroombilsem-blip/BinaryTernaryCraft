using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace BinaryTernaryCraft.Models
{
    public static class GeoJsonModelLoader
    {
        public static ParsedModel LoadFromFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            string fileName = Path.GetFileName(filePath);
            return LoadFromJson(json, fileName);
        }

        public static ParsedModel LoadFromJson(string jsonContent, string modelFileName = "")
        {
            var model = new ParsedModel();
            model.ModelName = Path.GetFileNameWithoutExtension(modelFileName);

            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("minecraft:geometry", out var geometryArray) &&
                !root.TryGetProperty("geometry", out geometryArray))
            {
                return model;
            }

            JsonElement geometryElement;
            if (geometryArray.ValueKind == JsonValueKind.Array && geometryArray.GetArrayLength() > 0)
            {
                geometryElement = geometryArray[0];
            }
            else if (geometryArray.ValueKind == JsonValueKind.Object)
            {
                geometryElement = geometryArray;
            }
            else
            {
                return model;
            }

            float texWidth = 64.0f;
            float texHeight = 64.0f;
            if (geometryElement.TryGetProperty("description", out var desc))
            {
                if (desc.TryGetProperty("texture_width", out var tw)) texWidth = tw.GetSingle();
                if (desc.TryGetProperty("texture_height", out var th)) texHeight = th.GetSingle();
            }

            if (!geometryElement.TryGetProperty("bones", out var bonesArray))
            {
                return model;
            }

            // Dual-mode Kinetic Classification Rule:
            // 1. If filename starts with "kinetic_" or model contains NO static bones ("base", "casing", "frame"),
            //    all bones are treated as Kinetic Mesh.
            // 2. Otherwise, bones named "base", "casing", "frame" are Static, while "shaft", "gear", "cog", "rotor", "kinetic_*" are Kinetic.
            bool fileIsKineticPrefix = modelFileName.StartsWith("kinetic_", StringComparison.OrdinalIgnoreCase);
            bool hasStaticBones = false;

            foreach (var bone in bonesArray.EnumerateArray())
            {
                string bName = bone.GetProperty("name").GetString()?.ToLowerInvariant() ?? "";
                if (bName.Contains("base") || bName.Contains("casing") || bName.Contains("frame"))
                {
                    hasStaticBones = true;
                    break;
                }
            }

            bool treatAllAsKinetic = fileIsKineticPrefix || !hasStaticBones;

            foreach (var bone in bonesArray.EnumerateArray())
            {
                string boneName = bone.GetProperty("name").GetString()?.ToLowerInvariant() ?? "";
                
                Vector3 bonePivot = new Vector3(8.0f, 8.0f, 8.0f); // Default center in Blockbench 16x16x16 space
                if (bone.TryGetProperty("pivot", out var pivotElem) && pivotElem.GetArrayLength() >= 3)
                {
                    bonePivot = new Vector3(
                        pivotElem[0].GetSingle(),
                        pivotElem[1].GetSingle(),
                        pivotElem[2].GetSingle()
                    );
                }

                // Convert pivot to 1x1x1 OpenGL voxel space
                Vector3 normalizedPivot = bonePivot * (1.0f / 16.0f);
                model.Pivot = normalizedPivot;

                bool isKineticBone = treatAllAsKinetic;
                if (!treatAllAsKinetic)
                {
                    if (boneName.Contains("shaft") || boneName.Contains("gear") || boneName.Contains("cog") ||
                        boneName.Contains("rotor") || boneName.Contains("head") || boneName.StartsWith("kinetic"))
                    {
                        isKineticBone = true;
                    }
                    else
                    {
                        isKineticBone = false;
                    }
                }

                if (!bone.TryGetProperty("cubes", out var cubesArray))
                {
                    continue;
                }

                var targetVertices = isKineticBone ? model.KineticVertices : model.StaticVertices;
                var targetIndices = isKineticBone ? model.KineticIndices : model.StaticIndices;

                foreach (var cube in cubesArray.EnumerateArray())
                {
                    AddCubeToMesh(cube, bonePivot, texWidth, texHeight, targetVertices, targetIndices);
                }
            }

            return model;
        }

        private static void AddCubeToMesh(
            JsonElement cube,
            Vector3 bonePivot,
            float texWidth,
            float texHeight,
            List<VoxelVertex> vertices,
            List<uint> indices)
        {
            var originElem = cube.GetProperty("origin");
            var sizeElem = cube.GetProperty("size");

            Vector3 origin = new Vector3(originElem[0].GetSingle(), originElem[1].GetSingle(), originElem[2].GetSingle());
            Vector3 size = new Vector3(sizeElem[0].GetSingle(), sizeElem[1].GetSingle(), sizeElem[2].GetSingle());

            // Convert Blockbench 16-scale to OpenGL unit 1-scale
            const float scale = 1.0f / 16.0f;
            Vector3 min = origin * scale;
            Vector3 max = (origin + size) * scale;

            float u = 0;
            float v = 0;
            if (cube.TryGetProperty("uv", out var uvElem) && uvElem.ValueKind == JsonValueKind.Array && uvElem.GetArrayLength() >= 2)
            {
                u = uvElem[0].GetSingle();
                v = uvElem[1].GetSingle();
            }

            // Build 6 faces: West(-X), East(+X), Down(-Y), Up(+Y), North(-Z), South(+Z)
            // Face 0: West (-X)
            AddFace(
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(min.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z),
                new Vector3(min.X, max.Y, min.Z),
                -Vector3.UnitX, u, v, size.Z, size.Y, texWidth, texHeight, vertices, indices);

            // Face 1: East (+X)
            AddFace(
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, max.Z),
                Vector3.UnitX, u, v, size.Z, size.Y, texWidth, texHeight, vertices, indices);

            // Face 2: Down (-Y)
            AddFace(
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(min.X, min.Y, max.Z),
                -Vector3.UnitY, u, v, size.X, size.Z, texWidth, texHeight, vertices, indices);

            // Face 3: Up (+Y)
            AddFace(
                new Vector3(min.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                Vector3.UnitY, u, v, size.X, size.Z, texWidth, texHeight, vertices, indices);

            // Face 4: North (-Z)
            AddFace(
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),
                -Vector3.UnitZ, u, v, size.X, size.Y, texWidth, texHeight, vertices, indices);

            // Face 5: South (+Z)
            AddFace(
                new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z),
                Vector3.UnitZ, u, v, size.X, size.Y, texWidth, texHeight, vertices, indices);
        }

        private static void AddFace(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 normal, float u, float v, float w, float h,
            float texWidth, float texHeight,
            List<VoxelVertex> vertices, List<uint> indices)
        {
            uint baseIndex = (uint)vertices.Count;

            float uvU0 = u / texWidth;
            float uvV0 = v / texHeight;
            float uvU1 = (u + w) / texWidth;
            float uvV1 = (v + h) / texHeight;

            Vector3 colorTint = new Vector3(0.9f, 0.9f, 0.9f);

            vertices.Add(new VoxelVertex(v0, normal, new Vector2(uvU0, uvV1), 0, colorTint));
            vertices.Add(new VoxelVertex(v1, normal, new Vector2(uvU1, uvV1), 0, colorTint));
            vertices.Add(new VoxelVertex(v2, normal, new Vector2(uvU1, uvV0), 0, colorTint));
            vertices.Add(new VoxelVertex(v3, normal, new Vector2(uvU0, uvV0), 0, colorTint));

            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
    }
}
