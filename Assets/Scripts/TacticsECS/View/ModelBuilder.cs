using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모델 파츠 표(ModelDefinition — Assets/Resources/Models/*.csv)를 GameObject 하나로 조립하는 표시 전용 헬퍼.
    /// 조각을 색별로 묶어 메시 하나(색마다 서브메시 하나)로 합치므로, 조각이 20개여도 렌더러는 1개, 드로우콜은 색 수만큼이다.
    /// 합친 메시는 (모델, 레벨)마다 한 번만 만들어 공유하고, 머티리얼도 색마다 하나를 공유한다. 콜라이더 없음.
    /// </summary>
    public static class ModelBuilder
    {
        private struct Built
        {
            public Mesh Mesh;
            public Color[] Colors;
            public bool[] Team;
        }

        private static readonly Dictionary<string, Built> MeshCache = new Dictionary<string, Built>();
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static int _cacheVersion = -1;

        public static bool Has(string modelId) => !string.IsNullOrEmpty(modelId) && ModelDefinition.Models.ContainsKey(modelId);

        /// <summary>modelId 모델을 parent 아래 localPosition에 만든다. level은 MinLevel/MaxLevel 조각 선택, teamColor는 Team 조각 색.
        /// 표에 없는 모델이면 null.</summary>
        public static GameObject Build(string modelId, Transform parent, Vector3 localPosition, int level = 1, Color? teamColor = null)
        {
            if (!ModelDefinition.Models.TryGetValue(modelId ?? string.Empty, out var parts)) return null;
            if (_cacheVersion != ModelDefinition.Version) { MeshCache.Clear(); _cacheVersion = ModelDefinition.Version; }
            string key = modelId + "#" + level;
            if (!MeshCache.TryGetValue(key, out var built) || built.Mesh == null)
            {
                built = Combine(parts, level);
                built.Mesh.name = "Model_" + key;
                MeshCache[key] = built;
            }

            var go = new GameObject("Model_" + modelId);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = built.Mesh;
            var mats = new Material[built.Colors.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = MaterialFor(built.Team[i] && teamColor.HasValue ? teamColor.Value : built.Colors[i]);
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        public static Material MaterialFor(Color c)
        {
            if (!Materials.TryGetValue(c, out var m) || m == null) Materials[c] = m = RuntimeMaterial.CreateColored(c);
            return m;
        }

        private static Built Combine(ModelPartInfo[] parts, int level)
        {
            // 색(팀 색 조각은 따로)별로 조각을 모은다 — 서브메시 순서 = 처음 나온 순서.
            var groups = new List<(Color Color, bool Team, List<CombineInstance> Items)>();
            foreach (var p in parts)
            {
                if (p.MinLevel > 0 && level < p.MinLevel) continue;
                if (p.MaxLevel > 0 && level > p.MaxLevel) continue;
                int gi = groups.FindIndex(g => g.Team == p.TeamColor && (p.TeamColor || g.Color == p.Color));
                if (gi < 0) { groups.Add((p.Color, p.TeamColor, new List<CombineInstance>())); gi = groups.Count - 1; }
                groups[gi].Items.Add(new CombineInstance
                {
                    mesh = LowPolyMeshes.Get(p.Shape),
                    transform = Matrix4x4.TRS(p.Position, Quaternion.Euler(p.Rotation), p.Size),
                });
            }

            var subMeshes = new CombineInstance[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                var m = new Mesh();
                m.CombineMeshes(groups[i].Items.ToArray(), true, true);
                subMeshes[i] = new CombineInstance { mesh = m, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh();
            mesh.CombineMeshes(subMeshes, false, false);
            mesh.RecalculateBounds();
            foreach (var s in subMeshes) Object.DestroyImmediate(s.mesh);

            var result = new Built { Mesh = mesh, Colors = new Color[groups.Count], Team = new bool[groups.Count] };
            for (int i = 0; i < groups.Count; i++) { result.Colors[i] = groups[i].Color; result.Team[i] = groups[i].Team; }
            return result;
        }
    }
}
