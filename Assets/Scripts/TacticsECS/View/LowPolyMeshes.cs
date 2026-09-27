using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모델 파츠(ModelShape)용 저폴리 단위 메시 — 전부 바닥 중심이 원점, 가로/세로/높이 1(y 0~1). 면마다 정점을 따로 둬
    /// 각진 음영(flat shading)이 나오므로 폴리토피아의 면 단위 음영과 같은 느낌이 난다. 표시 전용, 메시는 한 번만 만든다.
    /// </summary>
    public static class LowPolyMeshes
    {
        private const int Segments = 8;
        private static readonly Dictionary<ModelShape, Mesh> Cache = new Dictionary<ModelShape, Mesh>();

        public static Mesh Get(ModelShape shape)
        {
            if (Cache.TryGetValue(shape, out var m) && m != null) return m;
            m = shape switch
            {
                ModelShape.Gable => Gable(),
                ModelShape.Pyramid => Frustum(0f),
                ModelShape.Frustum => Frustum(0.6f),
                ModelShape.Cone => Round(0f),
                ModelShape.Cylinder => Round(1f),
                ModelShape.Tapered => Round(0.6f),
                ModelShape.Sphere => Sphere(),
                _ => Frustum(1f),
            };
            m.name = "LowPoly_" + shape;
            Cache[shape] = m;
            return m;
        }

        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) return;
                n.Normalize();
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            public Mesh ToMesh()
            {
                var tris = new int[V.Count];
                for (int i = 0; i < tris.Length; i++) tris[i] = i;
                var mesh = new Mesh { vertices = V.ToArray(), normals = N.ToArray(), triangles = tris };
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>사각 기둥/뿔대: 윗면 크기 = top(1 상자, 0.6 절두체, 0 사각뿔).</summary>
        private static Mesh Frustum(float top)
        {
            var b = new Builder();
            float h = 0.5f, t = 0.5f * top;
            var p = new[]
            {
                new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h),
                new Vector3(-t, 1, -t), new Vector3(t, 1, -t), new Vector3(t, 1, t), new Vector3(-t, 1, t),
            };
            if (top > 0f) b.Quad(p[4], p[7], p[6], p[5]);
            b.Quad(p[0], p[1], p[2], p[3]);
            b.Quad(p[0], p[4], p[5], p[1]);
            b.Quad(p[2], p[6], p[7], p[3]);
            b.Quad(p[3], p[7], p[4], p[0]);
            b.Quad(p[1], p[5], p[6], p[2]);
            return b.ToMesh();
        }

        /// <summary>박공지붕: 바닥 1x1, 용마루(높이 1)가 X축 방향.</summary>
        private static Mesh Gable()
        {
            var b = new Builder();
            var a0 = new Vector3(-0.5f, 0, -0.5f); var a1 = new Vector3(0.5f, 0, -0.5f);
            var c0 = new Vector3(-0.5f, 0, 0.5f); var c1 = new Vector3(0.5f, 0, 0.5f);
            var r0 = new Vector3(-0.5f, 1, 0); var r1 = new Vector3(0.5f, 1, 0);
            b.Quad(a0, r0, r1, a1);   // -Z 경사면
            b.Quad(c1, r1, r0, c0);   // +Z 경사면
            b.Tri(a0, c0, r0);        // -X 삼각벽
            b.Tri(a1, r1, c1);        // +X 삼각벽
            b.Quad(a0, a1, c1, c0);   // 바닥
            return b.ToMesh();
        }

        private static Vector3 Ring(int i, float r, float y)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / Segments;
            return new Vector3(Mathf.Cos(a) * 0.5f * r, y, Mathf.Sin(a) * 0.5f * r);
        }

        /// <summary>8각 원기둥/원뿔: 지름 1, 윗면 지름 = top.</summary>
        private static Mesh Round(float top)
        {
            var b = new Builder();
            for (int i = 0; i < Segments; i++)
            {
                var b0 = Ring(i, 1f, 0f); var b1 = Ring(i + 1, 1f, 0f);
                var t0 = Ring(i, top, 1f); var t1 = Ring(i + 1, top, 1f);
                if (top > 0f) { b.Quad(b0, t0, t1, b1); b.Tri(new Vector3(0, 1, 0), t1, t0); }
                else b.Tri(b0, new Vector3(0, 1, 0), b1);
                b.Tri(Vector3.zero, b0, b1);
            }
            return b.ToMesh();
        }

        private static Mesh Sphere()
        {
            const int Rings = 5;
            var b = new Builder();
            Vector3 P(int ring, int seg)
            {
                float phi = Mathf.PI * ring / Rings;
                float theta = seg * Mathf.PI * 2f / Segments;
                return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * 0.5f, 0.5f + Mathf.Cos(phi) * 0.5f, Mathf.Sin(phi) * Mathf.Sin(theta) * 0.5f);
            }
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Segments; s++)
                {
                    var a = P(r, s); var bb = P(r, s + 1); var c = P(r + 1, s + 1); var d = P(r + 1, s);
                    if (r == 0) b.Tri(a, c, d);
                    else if (r == Rings - 1) b.Tri(a, bb, d);
                    else b.Quad(a, bb, c, d);
                }
            return b.ToMesh();
        }
    }
}
