using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 타일 위 구조물(수도/유적/자원/불가사리/마을) 프리팹 6종을 만들어주는 1회성 배치 도구. TileAssetSetup과
    /// 같은 이유로 Unity CLI(-executeMethod)로만 실행한다(에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.StructureAssetSetup.GenerateAll
    ///
    /// 메시 출처(전부 CC0, Kenney):
    /// - Capital: Assets/Art/Castle/Kenney/tower-round-build-f.fbx (Tower Defense Kit, 이미 tile.fbx로 일부
    ///   임포트되어 있던 팩 전체를 다시 받아 추가한 성/타워 조각)
    /// - Village: Assets/Art/Castle/Kenney/wood-structure.fbx (Tower Defense Kit, Capital보다 소박한
    ///   나무 구조물로 구분)
    /// - Ruin: Assets/Art/Nature/Kenney/statue_columnDamaged.fbx (Nature Kit)
    /// - Resource_Food: Assets/Art/Nature/Kenney/mushroom_redGroup.fbx (Nature Kit)
    /// - Resource_Ore: Assets/Art/Nature/Kenney/rock_largeA.fbx (Nature Kit)
    /// - Starfish: Nature Kit에 적당한 기성 모델이 없어, 여기서 5각 별 모양 평면 메시를 직접 생성한다
    ///   (RuntimeSprite.CreateCircle처럼 절차적으로 만드는 선례를 3D 메시로 확장).
    ///
    /// 여기서 만드는 프리팹은 메시(모양)만 담고 있다 — 색은 TileAssetSetup과 같은 이유로
    /// RuntimeMaterial.CreateColored로 타입별 단색을 입힌다(프로젝트 전체가 저폴리+단색 스타일이라 텍스처
    /// 매핑 없이도 스타일이 일관됨).
    /// </summary>
    public static class StructureAssetSetup
    {
        private const string CapitalMeshPath = "Assets/Art/Castle/Kenney/tower-round-build-f.fbx";
        private const string VillageMeshPath = "Assets/Art/Castle/Kenney/wood-structure.fbx";
        private const string RuinMeshPath = "Assets/Art/Nature/Kenney/statue_columnDamaged.fbx";
        private const string ResourceFoodMeshPath = "Assets/Art/Nature/Kenney/mushroom_redGroup.fbx";
        private const string ResourceOreMeshPath = "Assets/Art/Nature/Kenney/rock_largeA.fbx";

        private const string PrefabFolder = "Assets/Prefabs/Structures";
        private const string MaterialFolder = "Assets/Materials";
        private const string StarMeshAssetPath = "Assets/Art/Nature/StarfishMesh.asset";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        private static readonly Color CapitalColor = new Color(0.85f, 0.7f, 0.15f);       // 금색 — 눈에 띄는 랜드마크
        private static readonly Color VillageColor = new Color(0.55f, 0.4f, 0.25f);       // 갈색 나무 — 수도보다 소박
        private static readonly Color RuinColor = new Color(0.6f, 0.6f, 0.6f);            // 회색 돌
        private static readonly Color ResourceFoodColor = new Color(0.8f, 0.25f, 0.3f);   // 붉은 버섯
        private static readonly Color ResourceOreColor = new Color(0.3f, 0.75f, 0.75f);   // 청록 광물(Rock 지형의 회색과 구분)
        private static readonly Color StarfishColor = new Color(0.9f, 0.5f, 0.2f);        // 주황 불가사리

        public static void GenerateAll()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Structures");
            EnsureFolder("Assets", "Materials");

            var capital = GenerateFromMesh("Structure_Capital", CapitalMeshPath, CapitalColor);
            var village = GenerateFromMesh("Structure_Village", VillageMeshPath, VillageColor);
            var ruin = GenerateFromMesh("Structure_Ruin", RuinMeshPath, RuinColor);
            var resourceFood = GenerateFromMesh("Structure_ResourceFood", ResourceFoodMeshPath, ResourceFoodColor);
            var resourceOre = GenerateFromMesh("Structure_ResourceOre", ResourceOreMeshPath, ResourceOreColor);
            var starfish = GenerateStarfishPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (capital != null && village != null && ruin != null && resourceFood != null && resourceOre != null && starfish != null)
                AssignToSandboxScene(capital, village, ruin, resourceFood, resourceOre, starfish);

            Debug.Log("[StructureAssetSetup] GenerateAll done");
        }

        private static GameObject GenerateFromMesh(string prefabName, string meshPath, Color color)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (mesh == null)
            {
                Debug.LogError($"[StructureAssetSetup] mesh not found: {meshPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(mesh);
            instance.name = prefabName;
            TintAllRenderers(instance, GenerateOrLoadMaterial(prefabName, color));

            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        /// <summary>단색 머티리얼을 디스크 에셋으로 저장해서 재사용한다 — RuntimeMaterial.CreateColored가
        /// 만드는 머티리얼은 메모리에만 존재하는 임시 오브젝트라, 프리팹에 참조만 넣고
        /// PrefabUtility.SaveAsPrefabAsset로 저장하면 에셋이 아닌 참조는 null로 직렬화되어 버린다(그 결과
        /// 렌더러가 null 머티리얼 상태가 되어 Unity 기본 마젠타로 보였다 — 처음 시도에서 겪은 버그).
        /// UIPrefabSetup.GenerateHpBarBackgroundMaterial과 같은 해결 패턴: 한 번 .mat 에셋으로 저장해두고
        /// 재실행 시에는 그대로 재사용한다.</summary>
        private static Material GenerateOrLoadMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = RuntimeMaterial.CreateColored(color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Nature Kit에 불가사리가 없어 5각 별 모양 평면 메시를 직접 만든다 — 중심에서 바깥/안쪽
        /// 정점을 번갈아 배치한 부채꼴(fan) 삼각분할. 타일 위에 놓일 정도로만 작게(반지름 0.5) 만든다.</summary>
        private static GameObject GenerateStarfishPrefab()
        {
            var mesh = BuildStarMesh(outerRadius: 0.5f, innerRadius: 0.22f, points: 5);
            AssetDatabase.CreateAsset(mesh, StarMeshAssetPath);

            var instance = new GameObject("Structure_Starfish");
            var filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GenerateOrLoadMaterial("Structure_Starfish", StarfishColor);

            string path = $"{PrefabFolder}/Structure_Starfish.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        private static Mesh BuildStarMesh(float outerRadius, float innerRadius, int points)
        {
            int vertCount = points * 2 + 1; // 중심 + (바깥/안쪽) x points
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;

            float angleStep = Mathf.PI / points; // 바깥/안쪽 정점이 번갈아 나오므로 한 뾰족점당 두 스텝
            for (int i = 0; i < points * 2; i++)
            {
                float angle = -Mathf.PI / 2f + i * angleStep;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                normals[i + 1] = Vector3.up;
            }

            var triangles = new int[points * 2 * 3];
            for (int i = 0; i < points * 2; i++)
            {
                int next = i + 1 == points * 2 ? 0 : i + 1;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            var mesh = new Mesh { name = "StarfishMesh" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>렌더러의 모든 머티리얼 슬롯을 같은 단색 머티리얼(에셋으로 저장된 것 — GenerateOrLoadMaterial
        /// 참고)로 덮어쓴다. 슬롯 0만 바꾸면(sharedMaterial 단수형 setter) Kenney 원본 FBX가 갖고 있던
        /// 나머지 슬롯(예: tower-round-build-f처럼 부위별로 서로 다른 머티리얼을 쓰는 모델)이 그대로
        /// 남는다.</summary>
        private static void TintAllRenderers(GameObject root, Material tinted)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = tinted;
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>Sandbox 씬의 BattleController에 구조물 프리팹 5종을 채워 넣는다(TileAssetSetup.AssignToScene과
        /// 같은 패턴). SampleScene은 지형 생성 자체를 쓰지 않아 배정하지 않는다.</summary>
        private static void AssignToSandboxScene(GameObject capital, GameObject village, GameObject ruin, GameObject resourceFood, GameObject resourceOre, GameObject starfish)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) == null) return;

            var scene = EditorSceneManager.OpenScene(SandboxScenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[StructureAssetSetup] BattleController not found in " + SandboxScenePath);
                return;
            }

            SetPrivateField(controller, "capitalStructurePrefab", capital);
            SetPrivateField(controller, "villageStructurePrefab", village);
            SetPrivateField(controller, "ruinStructurePrefab", ruin);
            SetPrivateField(controller, "resourceFoodStructurePrefab", resourceFood);
            SetPrivateField(controller, "resourceOreStructurePrefab", resourceOre);
            SetPrivateField(controller, "starfishStructurePrefab", starfish);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
