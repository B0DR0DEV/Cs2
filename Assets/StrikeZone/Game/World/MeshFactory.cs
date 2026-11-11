using System.Collections.Generic;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Generates the meshes used for blockout geometry. Box UVs are in metres (1 UV = 1 m)
    /// so textures keep a constant texel density on boxes of any size — that is what lets
    /// all walls share one material and batch together.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<long, Mesh> BoxCache = new Dictionary<long, Mesh>();
        private static Mesh _quad;

        public static Mesh Box(Vector3 size)
        {
            long key = Quantize(size.x) * 73856093L ^ Quantize(size.y) * 19349663L ^ Quantize(size.z) * 83492791L;
            Mesh mesh;
            if (BoxCache.TryGetValue(key, out mesh)) return mesh;

            Vector3 h = size * 0.5f;
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            int v = 0;
            int t = 0;
            // +X, -X, +Y, -Y, +Z, -Z faces.
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.right, Vector3.forward, Vector3.up, h, size.z, size.y);
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.left, Vector3.back, Vector3.up, h, size.z, size.y);
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.up, Vector3.right, Vector3.forward, h, size.x, size.z);
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.down, Vector3.right, Vector3.back, h, size.x, size.z);
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.forward, Vector3.left, Vector3.up, h, size.x, size.y);
            AddFace(vertices, normals, uvs, triangles, ref v, ref t, Vector3.back, Vector3.right, Vector3.up, h, size.x, size.y);

            mesh = new Mesh { name = "Box_" + size.x.ToString("0.##") + "x" + size.y.ToString("0.##") + "x" + size.z.ToString("0.##") };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            BoxCache.Add(key, mesh);
            return mesh;
        }

        /// <summary>Unit quad in the XY plane; its visible side faces -Z (orient with LookRotation(-normal)).</summary>
        public static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "SZ_Quad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        private static long Quantize(float f)
        {
            return (long)Mathf.Round(f * 1000f);
        }

        /// <summary>
        /// Adds one face. <paramref name="u"/> and <paramref name="w"/> span the face so that
        /// Cross(w, u) points along the normal (clockwise winding = front face in Unity).
        /// </summary>
        private static void AddFace(Vector3[] verts, Vector3[] norms, Vector2[] uvs, int[] tris, ref int v, ref int t,
            Vector3 normal, Vector3 u, Vector3 w, Vector3 half, float uSize, float wSize)
        {
            Vector3 center = Vector3.Scale(normal, half);
            Vector3 du = Vector3.Scale(u, half);
            Vector3 dw = Vector3.Scale(w, half);
            du = u * du.magnitude;
            dw = w * dw.magnitude;

            verts[v + 0] = center - du - dw;
            verts[v + 1] = center + du - dw;
            verts[v + 2] = center + du + dw;
            verts[v + 3] = center - du + dw;
            for (int i = 0; i < 4; i++) norms[v + i] = normal;
            uvs[v + 0] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(uSize, 0f);
            uvs[v + 2] = new Vector2(uSize, wSize);
            uvs[v + 3] = new Vector2(0f, wSize);

            // Unity treats triangle (a, b, c) as facing along Cross(b - a, c - a).
            // Pick the winding whose facing matches the face normal.
            Vector3 triNormal = Vector3.Cross(verts[v + 1] - verts[v + 0], verts[v + 2] - verts[v + 0]);
            if (Vector3.Dot(triNormal, normal) > 0f)
            {
                tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + 2;
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 3;
            }
            else
            {
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                tris[t++] = v; tris[t++] = v + 3; tris[t++] = v + 2;
            }
            v += 4;
        }
    }
}
