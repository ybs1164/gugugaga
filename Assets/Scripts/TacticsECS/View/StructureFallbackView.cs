using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 전용 프리팹이 없는 구조물(등대/물고기, 9차 재정비에서 추가)의 간이 모델을 코드로 만든다 —
    /// TerrainFeatureView와 같은 방식(에셋 없이 프리미티브만 조립, 머티리얼은 한 번만 만들어 공유).
    /// 순수 표시용이라 콜라이더는 제거한다(타일 클릭 판정은 지면 평면 레이캐스트라 영향 없음).
    /// </summary>
    public static class StructureFallbackView
    {
        private static Material _towerMaterial;
        private static Material _lampMaterial;
        private static Material _fishMaterial;

        /// <summary>structureId에 맞는 간이 모델을 parent(타일) 아래에 만들어 반환한다. 모르는 Id면 null.</summary>
        public static GameObject Create(string structureId, Transform parent, float baseHeight)
        {
            if (structureId != StructureGenerationSystem.LighthouseStructureId && structureId != "Resource_Fish") return null;
            EnsureMaterials();

            var root = new GameObject("Structure_" + structureId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, baseHeight, 0f);

            if (structureId == StructureGenerationSystem.LighthouseStructureId)
            {
                AddPrimitive(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f), _towerMaterial);
                AddPrimitive(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0f), new Vector3(0.18f, 0.12f, 0.18f), _lampMaterial);
            }
            else
            {
                // 물 타일(파랑) 위에서 눈에 띄도록 밝은 노란색 물고기 세 마리 — 물 타일 메시 윗면보다 높게 띄운다.
                AddPrimitive(root.transform, PrimitiveType.Sphere, new Vector3(-0.15f, 0.22f, 0.12f), new Vector3(0.3f, 0.1f, 0.14f), _fishMaterial);
                AddPrimitive(root.transform, PrimitiveType.Sphere, new Vector3(0.16f, 0.22f, 0.02f), new Vector3(0.26f, 0.09f, 0.12f), _fishMaterial);
                AddPrimitive(root.transform, PrimitiveType.Sphere, new Vector3(-0.02f, 0.22f, -0.18f), new Vector3(0.24f, 0.09f, 0.11f), _fishMaterial);
            }
            return root;
        }

        private static void AddPrimitive(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void EnsureMaterials()
        {
            if (_towerMaterial == null) _towerMaterial = RuntimeMaterial.CreateColored(new Color(0.95f, 0.95f, 0.92f));
            if (_lampMaterial == null) _lampMaterial = RuntimeMaterial.CreateColored(new Color(0.9f, 0.2f, 0.15f));
            if (_fishMaterial == null) _fishMaterial = RuntimeMaterial.CreateColored(new Color(1f, 0.85f, 0.3f));
        }
    }
}
