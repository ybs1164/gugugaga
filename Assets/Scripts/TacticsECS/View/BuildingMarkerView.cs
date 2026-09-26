using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 타일 건물(TileData.BuildingId)과 도시 주인 표시를 코드로 조립하는 표시 전용 헬퍼 — TerrainFeatureView와
    /// 같은 방식(별도 에셋 없이 프리미티브 조각, 콜라이더 없음, 머티리얼 공유). 건물 전용 모델 에셋이 아직 없어
    /// 건물마다 색/실루엣이 다른 블록 조합으로 구분만 한다(모델이 생기면 이 표만 프리팹으로 바꾸면 된다).
    /// </summary>
    public static class BuildingMarkerView
    {
        /// <summary>조각 하나: 위치/크기(타일 로컬, 타일 한 변 = 1), 색, 모양.</summary>
        private struct Piece
        {
            public Vector3 Pos;
            public Vector3 Size;
            public Color Color;
            public PrimitiveType Shape;
            public Piece(float x, float y, float z, float sx, float sy, float sz, Color c, PrimitiveType shape = PrimitiveType.Cube)
            { Pos = new Vector3(x, y, z); Size = new Vector3(sx, sy, sz); Color = c; Shape = shape; }
        }

        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color Stone = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color White = new Color(0.93f, 0.92f, 0.88f);
        private static readonly Color Roof = new Color(0.78f, 0.33f, 0.24f);

        private static readonly Dictionary<string, Piece[]> Shapes = new Dictionary<string, Piece[]>
        {
            ["Farm"] = new[] { new Piece(0f, 0.02f, 0f, 0.8f, 0.04f, 0.8f, new Color(0.85f, 0.75f, 0.30f)),
                               new Piece(-0.2f, 0.06f, 0f, 0.08f, 0.06f, 0.75f, new Color(0.60f, 0.70f, 0.25f)),
                               new Piece(0.2f, 0.06f, 0f, 0.08f, 0.06f, 0.75f, new Color(0.60f, 0.70f, 0.25f)) },
            ["Mine"] = new[] { new Piece(0f, 0.12f, 0.1f, 0.4f, 0.24f, 0.3f, new Color(0.30f, 0.30f, 0.32f)),
                               new Piece(0f, 0.08f, -0.08f, 0.2f, 0.16f, 0.08f, new Color(0.10f, 0.10f, 0.10f)),
                               new Piece(0.22f, 0.05f, -0.2f, 0.12f, 0.1f, 0.12f, new Color(0.35f, 0.75f, 0.75f)) },
            ["LumberHut"] = new[] { new Piece(0f, 0.1f, 0f, 0.36f, 0.2f, 0.3f, Wood),
                                    new Piece(0f, 0.24f, 0f, 0.42f, 0.08f, 0.36f, new Color(0.35f, 0.24f, 0.14f)) },
            ["Windmill"] = new[] { new Piece(0f, 0.25f, 0f, 0.22f, 0.25f, 0.22f, White, PrimitiveType.Cylinder),
                                   new Piece(0f, 0.5f, -0.13f, 0.06f, 0.5f, 0.02f, Wood),
                                   new Piece(0f, 0.5f, -0.13f, 0.5f, 0.06f, 0.02f, Wood) },
            ["Forge"] = new[] { new Piece(0f, 0.12f, 0f, 0.42f, 0.24f, 0.34f, new Color(0.45f, 0.20f, 0.16f)),
                                new Piece(0.13f, 0.34f, 0.08f, 0.1f, 0.24f, 0.1f, Stone),
                                new Piece(-0.08f, 0.1f, -0.18f, 0.14f, 0.1f, 0.02f, new Color(1f, 0.55f, 0.1f)) },
            ["Sawmill"] = new[] { new Piece(0f, 0.1f, 0f, 0.5f, 0.2f, 0.28f, new Color(0.72f, 0.55f, 0.35f)),
                                  new Piece(0f, 0.08f, -0.24f, 0.46f, 0.08f, 0.1f, Wood) },
            ["Market"] = new[] { new Piece(0f, 0.1f, 0f, 0.44f, 0.2f, 0.3f, White),
                                 new Piece(0f, 0.23f, -0.06f, 0.5f, 0.04f, 0.4f, new Color(0.25f, 0.55f, 0.90f)) },
            ["Port"] = new[] { new Piece(0f, 0.03f, 0f, 0.7f, 0.06f, 0.24f, Wood),
                               new Piece(-0.28f, 0.1f, 0f, 0.06f, 0.2f, 0.06f, Wood),
                               new Piece(0.28f, 0.1f, 0f, 0.06f, 0.2f, 0.06f, Wood) },
            ["Temple"] = Temple(White),
            ["ForestTemple"] = Temple(new Color(0.55f, 0.80f, 0.45f)),
            ["MountainTemple"] = Temple(new Color(0.70f, 0.70f, 0.75f)),
            ["WaterTemple"] = Temple(new Color(0.45f, 0.75f, 0.95f)),
        };

        private static Piece[] Temple(Color c) => new[]
        {
            new Piece(0f, 0.05f, 0f, 0.6f, 0.1f, 0.6f, c),
            new Piece(0f, 0.15f, 0f, 0.44f, 0.1f, 0.44f, c),
            new Piece(0f, 0.27f, 0f, 0.3f, 0.14f, 0.3f, Roof),
        };

        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        /// <summary>buildingId 모델을 parent(타일) 아래 baseHeight 높이에 만든다. 표에 없는 Id면 null.</summary>
        public static GameObject CreateBuilding(string buildingId, Transform parent, float baseHeight)
        {
            if (string.IsNullOrEmpty(buildingId) || !Shapes.TryGetValue(buildingId, out var pieces)) return null;
            var root = new GameObject("Building_" + buildingId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, baseHeight, 0f);
            foreach (var p in pieces) AddPiece(root.transform, p);
            return root;
        }

        /// <summary>도시 칸 발밑에 까는 팀 색 원판(+ 레벨만큼의 작은 기둥 눈금) — 누가 이 도시를 가졌는지와
        /// 레벨을 멀리서도 보이게 한다.</summary>
        public static GameObject CreateCityBase(Color teamColor, int level, Transform parent, float baseHeight)
        {
            var root = new GameObject("CityBase");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, baseHeight, 0f);
            AddPiece(root.transform, new Piece(0f, 0.005f, 0f, 0.98f, 0.005f, 0.98f, teamColor, PrimitiveType.Cylinder));
            int pips = Mathf.Min(level, 8);
            for (int i = 0; i < pips; i++)
                AddPiece(root.transform, new Piece(-0.42f + i * 0.1f, 0.05f, -0.46f, 0.07f, 0.1f, 0.07f, teamColor));
            return root;
        }

        /// <summary>영토 경계 테두리: edges의 각 방향(타일 로컬 +X/-X/+Z/-Z 단위 벡터)마다 그 변을 따라 얇은 팀 색
        /// 막대를 깐다(폴리토피아의 국경선).</summary>
        public static void AddBorders(Transform parent, System.Collections.Generic.IEnumerable<Vector2Int> edges, Color teamColor)
        {
            const float thickness = 0.06f;
            const float inset = 0.5f - thickness * 0.5f;
            foreach (var e in edges)
            {
                bool alongX = e.y != 0; // 위/아래 변이면 X축을 따라 길다.
                AddPiece(parent, new Piece(e.x * inset, 0.012f, e.y * inset,
                    alongX ? 1f : thickness, 0.02f, alongX ? thickness : 1f, teamColor));
            }
        }

        private static void AddPiece(Transform parent, Piece p)
        {
            var go = GameObject.CreatePrimitive(p.Shape);
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.name = p.Shape.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p.Pos;
            go.transform.localScale = p.Size;
            if (!Materials.TryGetValue(p.Color, out var mat) || mat == null)
            {
                mat = RuntimeMaterial.CreateColored(p.Color);
                Materials[p.Color] = mat;
            }
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
