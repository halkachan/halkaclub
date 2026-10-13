using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Flat-shaded, vertex-colored meshes generated from Grid data (ground, walls and the
    // boxes that stand in for objects that have no Blender model yet).
    public sealed class PocMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public int TriangleCount => triangles.Count / 3;

        public void Quad(Vector3 min, Vector3 max, Color color) =>
            Face(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), Vector3.up, color);

        public void Box(Vector3 min, Vector3 max, Color side, Color top)
        {
            Face(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up, top);
            Face(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back, side);
            Face(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.forward, side);
            Face(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left, side);
            Face(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right, side);
        }

        // Vertices are given clockwise as seen from the outside (Unity front faces).
        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (var i = 0; i < 4; i++) { normals.Add(normal); colors.Add(color); }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        public Mesh CreateMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public GameObject CreateObject(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = CreateMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
