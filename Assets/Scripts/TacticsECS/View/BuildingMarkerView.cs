using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 타일 건물(TileData.BuildingId)과 도시를 그리는 표시 전용 헬퍼. 모양은 코드가 아니라 모델 파츠 CSV
    /// (Assets/Resources/Models/BuildingModels.csv — 모델 Id "Building.&lt;건물 Id&gt;", 도시는 "City.*")에 있고,
    /// ModelBuilder가 조립한다(1차 모델링 비교분석 — docs/ModelingPlan.md). 레벨이 있는 건물(신전/가공 건물/시장)은
    /// 레벨에 따라 조각이 늘어난다(위키: 제재소 창문 수 = 레벨, 대장간 레벨 0은 불 꺼짐 등). 영토 경계선/도시 원판만 코드로 그린다.
    /// </summary>
    public static class BuildingMarkerView
    {
        /// <summary>조각 하나: 위치/크기(타일 로컬, 타일 한 변 = 1), 색, 모양 — 경계선/도시 원판 전용.</summary>
        private struct Piece
        {
            public Vector3 Pos;
            public Vector3 Size;
            public Color Color;
            public PrimitiveType Shape;
            public Piece(float x, float y, float z, float sx, float sy, float sz, Color c, PrimitiveType shape = PrimitiveType.Cube)
            { Pos = new Vector3(x, y, z); Size = new Vector3(sx, sy, sz); Color = c; Shape = shape; }
        }

        public const string BuildingModelPrefix = "Building.";

        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        /// <summary>buildingId 모델을 parent(타일) 아래 baseHeight 높이에 만든다. 모델 표에 없으면 null. level은 표시 레벨
        /// (TileImprovementSystem.DisplayLevel), rotate90은 다리를 Z축(상하 육지) 방향으로 돌릴 때, teamColor는 깃발 같은 팀 색 조각.</summary>
        public static GameObject CreateBuilding(string buildingId, Transform parent, float baseHeight, int level = 1, bool rotate90 = false, Color? teamColor = null)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            var go = ModelBuilder.Build(BuildingModelPrefix + buildingId, parent, new Vector3(0f, baseHeight, 0f), level, teamColor);
            if (go == null) return null;
            go.name = "Building_" + buildingId;
            if (rotate90) go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return go;
        }

        /// <summary>도시 모델: 팀 색 원판 + 레벨 눈금 + 레벨만큼의 집(City.Houses) + 수도 성/성벽/공방/공원(해당하면) + 팀 깃발.
        /// 위키 City — 도시는 레벨이 오를수록 건물이 늘고, 성벽은 도시 둘레에, 공방·공원은 도시 건물 사이에 보인다.</summary>
        public static GameObject CreateCity(CityData city, Color teamColor, Transform parent, float baseHeight)
        {
            var root = CreateCityBase(teamColor, city.Level, parent, baseHeight);
            ModelBuilder.Build("City.Houses", root.transform, Vector3.zero, city.Level, teamColor);
            if (city.IsCapital) ModelBuilder.Build("City.Capital", root.transform, Vector3.zero, 1, teamColor);
            if (city.HasWall) ModelBuilder.Build("City.Wall", root.transform, Vector3.zero, 1, teamColor);
            if (city.HasWorkshop) ModelBuilder.Build("City.Workshop", root.transform, Vector3.zero, 1, teamColor);
            if (city.ParkCount > 0) ModelBuilder.Build("City.Park", root.transform, Vector3.zero, 1, teamColor);
            ModelBuilder.Build("City.Flag", root.transform, Vector3.zero, 1, teamColor);
            root.name = "City";
            return root;
        }

        /// <summary>도시 칸 발밑에 까는 팀 색 원판(+ 레벨만큼의 작은 기둥 눈금) — 누가 이 도시를 가졌는지와
        /// 레벨을 멀리서도 보이게 한다.</summary>
        public static GameObject CreateCityBase(Color teamColor, int level, Transform parent, float baseHeight)
        {
            var root = new GameObject("CityBase");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, baseHeight, 0f);
            // 위키 도시엔 발밑 원판이 없다 — 주인은 깃발/국경선으로 보이므로 원판은 가장자리 얇은 테두리만 남긴다.
            AddPiece(root.transform, new Piece(0f, 0.004f, 0f, 0.98f, 0.004f, 0.98f, Color.Lerp(teamColor, Color.white, 0.55f), PrimitiveType.Cylinder));
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
