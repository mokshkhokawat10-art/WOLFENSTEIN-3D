using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wolfenstein.World
{
    /// <summary>
    /// Collects simple shapes, sorted by material, and turns them into one mesh with a submesh per material,
    /// so a great deal of level detail costs a single renderer. Texture coordinates are measured in world units,
    /// so a texture keeps the same scale however long or tall a shape is.
    /// </summary>
    public sealed class MeshBatch
    {
        private sealed class Section
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        private static readonly Vector3[] BoxFaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        private readonly Dictionary<Material, Section> sections = new Dictionary<Material, Section>();
        private readonly List<Material> order = new List<Material>();
        private readonly float uvScale;

        /// <param name="metresPerTexture">How many metres one repeat of a texture spans.</param>
        public MeshBatch(float metresPerTexture = 1f) => uvScale = 1f / Mathf.Max(0.01f, metresPerTexture);

        public bool IsEmpty => order.Count == 0;

        /// <summary>
        /// A flat rectangle facing along its normal, centred on a point. The side vector gives the direction of its width.
        /// </summary>
        public void AddQuad(Material material, Vector3 centre, Vector3 normal, Vector3 side, float width, float height)
        {
            Section section = GetSection(material);
            Vector3 up = Vector3.Cross(normal, side).normalized * (height * 0.5f);
            Vector3 across = side.normalized * (width * 0.5f);

            int start = section.Vertices.Count;
            Vector3[] corners = { centre - across - up, centre + across - up, centre + across + up, centre - across + up };
            foreach (Vector3 corner in corners)
            {
                section.Vertices.Add(corner);
                section.Normals.Add(normal);
                section.Uvs.Add(PlanarUv(corner, normal));
            }

            // Wound so the face is seen from the side its normal points to.
            AddFace(section, start, Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), normal) < 0f);
        }

        /// <summary>A box with its own rotation, textured on every face.</summary>
        public void AddBox(Material material, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            if (material == null)
                return;

            Vector3 half = size * 0.5f;
            foreach (Vector3 localNormal in BoxFaceNormals)
            {
                Vector3 localSide = Mathf.Abs(localNormal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, localNormal);
                Vector3 localUp = Vector3.Cross(localNormal, localSide);
                float width = Mathf.Abs(Vector3.Dot(localSide, size));
                float height = Mathf.Abs(Vector3.Dot(localUp, size));
                Vector3 faceCentre = centre + rotation * Vector3.Scale(localNormal, half);
                AddQuad(material, faceCentre, rotation * localNormal, rotation * localSide, width, height);
            }
        }

        /// <summary>
        /// A cylinder of a few flat sides between two points, closed at both ends. Coarse on purpose, for a retro look.
        /// </summary>
        public void AddCylinder(Material material, Vector3 from, Vector3 to, float radius, int sides = 8)
        {
            if (material == null)
                return;

            Section section = GetSection(material);
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.0001f)
                return;

            Vector3 direction = axis / length;
            Vector3 across = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 around = Vector3.Cross(direction, across);
            float circumference = 2f * Mathf.PI * radius;

            int start = section.Vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                Vector3 normal = across * Mathf.Cos(angle) + around * Mathf.Sin(angle);
                float u = circumference * i / sides * uvScale;
                section.Vertices.Add(from + normal * radius);
                section.Normals.Add(normal);
                section.Uvs.Add(new Vector2(u, 0f));
                section.Vertices.Add(to + normal * radius);
                section.Normals.Add(normal);
                section.Uvs.Add(new Vector2(u, length * uvScale));
            }

            for (int i = 0; i < sides; i++)
            {
                int a = start + i * 2;
                section.Triangles.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
            }

            AddCap(section, from, -direction, across, around, radius, sides);
            AddCap(section, to, direction, across, around, radius, sides);
        }

        /// <summary>
        /// Builds the collected shapes into a mesh on a new child of the parent, drawn without casting shadows.
        /// Returns null if nothing was collected.
        /// </summary>
        public MeshRenderer Build(string objectName, Transform parent)
        {
            if (IsEmpty)
                return null;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var mesh = new Mesh { name = objectName };
            foreach (Material material in order)
            {
                Section section = sections[material];
                vertices.AddRange(section.Vertices);
                normals.AddRange(section.Normals);
                uvs.AddRange(section.Uvs);
            }

            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = order.Count;

            int offset = 0;
            for (int i = 0; i < order.Count; i++)
            {
                Section section = sections[order[i]];
                mesh.SetTriangles(section.Triangles, i, true, offset);
                offset += section.Vertices.Count;
            }

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = holder.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = order.ToArray();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return meshRenderer;
        }

        private Section GetSection(Material material)
        {
            if (!sections.TryGetValue(material, out Section section))
            {
                section = new Section();
                sections.Add(material, section);
                order.Add(material);
            }

            return section;
        }

        private static void AddFace(Section section, int start, bool flip)
        {
            if (flip)
                section.Triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            else
                section.Triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private void AddCap(Section section, Vector3 centre, Vector3 normal, Vector3 across, Vector3 around, float radius, int sides)
        {
            int start = section.Vertices.Count;
            section.Vertices.Add(centre);
            section.Normals.Add(normal);
            section.Uvs.Add(Vector2.zero);
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                Vector3 offset = (across * Mathf.Cos(angle) + around * Mathf.Sin(angle)) * radius;
                section.Vertices.Add(centre + offset);
                section.Normals.Add(normal);
                section.Uvs.Add(new Vector2(offset.x + offset.z, offset.y) * uvScale);
            }

            for (int i = 0; i < sides; i++)
            {
                int a = start + 1 + i;
                int b = start + 1 + (i + 1) % sides;
                Vector3 faceNormal = Vector3.Cross(section.Vertices[a] - centre, section.Vertices[b] - centre);
                if (Vector3.Dot(faceNormal, normal) >= 0f)
                    section.Triangles.AddRange(new[] { start, a, b });
                else
                    section.Triangles.AddRange(new[] { start, b, a });
            }
        }

        // Projects a point onto the face's own plane, so textures run the same way on every wall, floor and ceiling.
        private Vector2 PlanarUv(Vector3 point, Vector3 normal)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Vector2 uv;
            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
                uv = new Vector2(point.x, point.z);
            else if (absolute.x >= absolute.z)
                uv = new Vector2(point.z * Mathf.Sign(normal.x), point.y);
            else
                uv = new Vector2(-point.x * Mathf.Sign(normal.z), point.y);

            return uv * uvScale;
        }
    }
}
