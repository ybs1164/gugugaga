using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 숲/산 타일(TerrainGenerationSystem.ForestTileId/MountainTileId) 위에 얹는 장식 모델을 코드로 만든다 —
    /// 별도 에셋 없이 원뿔 메시(로우폴리)만으로 나무 3그루/산 봉우리를 조립한다. 순수 표시용이라 콜라이더가
    /// 없다(타일 클릭 판정은 BattleController의 지면 평면 레이캐스트라 영향 없음). 메시/머티리얼은 모든 타일이
    /// 공유하도록 한 번만 만든다.
    /// </summary>
    public static class TerrainFeatureView
    {
        private const int ConeSides = 8;

        private static Mesh _coneMesh;
        private static Material _trunkMaterial;
        private static Material _foliageMaterial;
        private static Material _rockMaterial;
        private static Material _snowMaterial;

        /// <summary>tileTypeId에 맞는 장식을 parent(타일) 아래에 만들어 반환한다. 숲/산이 아니면 null.</summary>
        public static GameObject Create(string tileTypeId, Transform parent, Vector2Int gridPos, float baseHeight)
        {
            if (tileTypeId != TerrainGenerationSystem.ForestTileId && tileTypeId != TerrainGenerationSystem.MountainTileId) return null;
            EnsureResources();

            var root = new GameObject("Feature_" + tileTypeId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, baseHeight, 0f);
            // 칸마다 방향을 살짝 달리해 같은 모양이 격자처럼 반복돼 보이지 않게 한다(위치 기반이라 재생성해도 같음).
            root.transform.localRotation = Quaternion.Euler(0f, (gridPos.x * 73 + gridPos.y * 151) % 360, 0f);

            if (tileTypeId == TerrainGenerationSystem.ForestTileId)
            {
                AddTree(root.transform, new Vector3(-0.22f, 0f, -0.18f), 1.0f);
                AddTree(root.transform, new Vector3(0.21f, 0f, -0.08f), 0.85f);
                AddTree(root.transform, new Vector3(-0.03f, 0f, 0.24f), 0.95f);
            }
            else
            {
                AddCone(root.transform, new Vector3(-0.06f, 0f, 0.06f), 0.40f, 0.62f, _rockMaterial);
                AddCone(root.transform, new Vector3(-0.06f, 0.62f * 0.68f, 0.06f), 0.40f * 0.32f, 0.62f * 0.32f, _snowMaterial);
                AddCone(root.transform, new Vector3(0.24f, 0f, -0.22f), 0.24f, 0.36f, _rockMaterial);
            }
            return root;
        }

        private static void AddTree(Transform parent, Vector3 localPos, float scale)
        {
            AddCone(parent, localPos, 0.045f * scale, 0.14f * scale, _trunkMaterial);
            AddCone(parent, localPos + new Vector3(0f, 0.08f * scale, 0f), 0.15f * scale, 0.40f * scale, _foliageMaterial);
        }

        private static void AddCone(Transform parent, Vector3 localPos, float radius, float height, Material material)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = _coneMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void EnsureResources()
        {
            if (_coneMesh == null) _coneMesh = BuildUnitCone();
            if (_trunkMaterial == null) _trunkMaterial = RuntimeMaterial.CreateColored(new Color(0.40f, 0.27f, 0.16f));
            if (_foliageMaterial == null) _foliageMaterial = RuntimeMaterial.CreateColored(new Color(0.13f, 0.40f, 0.18f));
            if (_rockMaterial == null) _rockMaterial = RuntimeMaterial.CreateColored(new Color(0.46f, 0.44f, 0.42f));
            if (_snowMaterial == null) _snowMaterial = RuntimeMaterial.CreateColored(new Color(0.94f, 0.95f, 0.97f));
        }

        /// <summary>반지름 1, 높이 1(바닥 y=0, 꼭짓점 y=1)인 옆면만 있는 원뿔. 면마다 정점을 따로 둬서 로우폴리처럼
        /// 각진 음영(flat shading)이 나오게 한다.</summary>
        private static Mesh BuildUnitCone()
        {
            var vertices = new Vector3[ConeSides * 3];
            var triangles = new int[ConeSides * 3];
            var apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < ConeSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / ConeSides;
                float a1 = (i + 1) * Mathf.PI * 2f / ConeSides;
                vertices[i * 3] = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                vertices[i * 3 + 1] = apex;
                vertices[i * 3 + 2] = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                triangles[i * 3] = i * 3;
                triangles[i * 3 + 1] = i * 3 + 1;
                triangles[i * 3 + 2] = i * 3 + 2;
            }
            var mesh = new Mesh { name = "FeatureCone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
