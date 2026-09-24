using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 구조물 프리팹 조립용 단위 저폴리 메시(상자/원기둥/원뿔/구)를 절차적으로 만들어 에셋으로 저장한다 —
    /// StructureAssetSetup이 과일/작물/사냥감/물고기/등대처럼 Kenney 기성 모델이 없는 구조물을 이 조각들의
    /// 조합으로 만든다(불가사리 별 메시와 같은 선례). 모든 면이 정점을 따로 가져 각진 음영(flat shading)이
    /// 나오므로 TerrainFeatureView의 원뿔 숲/산, Kenney 저폴리 모델과 스타일이 맞는다.
    /// 전부 "단위" 크기라 조립할 때 Transform 스케일로 크기를 정한다:
    /// - Box: 한 변 1, 중심이 원점.
    /// - Cylinder/TaperedCylinder: 반지름 1, 높이 1, 바닥 y=0(윗면 반지름만 다름).
    /// - Cone: 반지름 1, 높이 1, 바닥 y=0, 꼭짓점 y=1.
    /// - Sphere: 반지름 1, 중심이 원점(8각 x 5단 저폴리).
    /// </summary>
    public static class ProceduralPropMeshes
    {
        private const string MeshFolder = "Assets/Art/Procedural";
        private const int Segments = 8;

        public static Mesh Box() => LoadOrCreate("PropBox", BuildBox);
        public static Mesh Cylinder() => LoadOrCreate("PropCylinder", () => BuildTaperedCylinder(1f));
        /// <summary>윗면 반지름이 바닥의 70%인 원기둥(등대 탑).</summary>
        public static Mesh TaperedCylinder() => LoadOrCreate("PropTaperedCylinder", () => BuildTaperedCylinder(0.7f));
        public static Mesh Cone() => LoadOrCreate("PropCone", BuildCone);
        public static Mesh Sphere() => LoadOrCreate("PropSphere", BuildSphere);

        /// <summary>같은 이름의 메시 에셋이 있으면 내용만 새로 채워(GUID 유지 — 이미 그 메시를 참조하는 프리팹이
        /// 깨지지 않게) 반환하고, 없으면 새로 만든다.</summary>
        private static Mesh LoadOrCreate(string name, System.Func<Mesh> build)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/Art", "Procedural");
            string path = $"{MeshFolder}/{name}.asset";
            var built = build();
            built.name = name;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.triangles = built.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        /// <summary>삼각형마다 정점 3개를 따로 두고 면 법선을 넣는 flat shading 메시 조립기.</summary>
        private sealed class FlatMeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
                _normals.Add(n); _normals.Add(n); _normals.Add(n);
            }

            /// <summary>a-b-c-d 순서(바깥에서 봤을 때 시계 방향)의 사각형.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh ToMesh()
            {
                var triangles = new int[_vertices.Count];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
                var mesh = new Mesh { vertices = _vertices.ToArray(), normals = _normals.ToArray(), triangles = triangles };
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Mesh BuildBox()
        {
            var m = new FlatMeshBuilder();
            float h = 0.5f;
            var p = new[]
            {
                new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h),
                new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(-h, h, h),
            };
            m.Quad(p[4], p[7], p[6], p[5]); // 위
            m.Quad(p[0], p[1], p[2], p[3]); // 아래
            m.Quad(p[0], p[4], p[5], p[1]); // -Z
            m.Quad(p[2], p[6], p[7], p[3]); // +Z
            m.Quad(p[3], p[7], p[4], p[0]); // -X
            m.Quad(p[1], p[5], p[6], p[2]); // +X
            return m.ToMesh();
        }

        private static Vector3 Ring(int i, float radius, float y)
        {
            float a = i * Mathf.PI * 2f / Segments;
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }

        private static Mesh BuildTaperedCylinder(float topRatio)
        {
            var m = new FlatMeshBuilder();
            var bottomCenter = Vector3.zero;
            var topCenter = Vector3.up;
            for (int i = 0; i < Segments; i++)
            {
                var b0 = Ring(i, 1f, 0f);
                var b1 = Ring(i + 1, 1f, 0f);
                var t0 = Ring(i, topRatio, 1f);
                var t1 = Ring(i + 1, topRatio, 1f);
                m.Quad(b0, t0, t1, b1);
                m.Tri(topCenter, t1, t0);
                m.Tri(bottomCenter, b0, b1);
            }
            return m.ToMesh();
        }

        private static Mesh BuildCone()
        {
            var m = new FlatMeshBuilder();
            var apex = Vector3.up;
            for (int i = 0; i < Segments; i++)
            {
                var b0 = Ring(i, 1f, 0f);
                var b1 = Ring(i + 1, 1f, 0f);
                m.Tri(b0, apex, b1);
                m.Tri(Vector3.zero, b0, b1);
            }
            return m.ToMesh();
        }

        private static Mesh BuildSphere()
        {
            const int Rings = 5;
            var m = new FlatMeshBuilder();
            Vector3 Point(int ring, int seg)
            {
                float phi = Mathf.PI * ring / Rings;          // 0(위) ~ PI(아래)
                float theta = seg * Mathf.PI * 2f / Segments;
                return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            }
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Segments; s++)
                {
                    var a = Point(r, s);
                    var b = Point(r, s + 1);
                    var c = Point(r + 1, s + 1);
                    var d = Point(r + 1, s);
                    if (r == 0) m.Tri(a, c, d);                 // 위 꼭짓점 부채꼴
                    else if (r == Rings - 1) m.Tri(a, b, d);    // 아래 꼭짓점 부채꼴
                    else m.Quad(a, b, c, d);
                }
            return m.ToMesh();
        }
    }
}
