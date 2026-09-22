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
    /// - Capital: Assets/Art/Castle/Kenney/tower-round-build-f.fbx 하나 — 실측 발자국이 이미 1x1 타일
    ///   전체를 채워서(bounds size=(1,1.79,1)) 그대로 랜드마크로 쓴다.
    /// - Village: 에셋에 "집"처럼 보이는 기성 모델이 없어(wood-structure.fbx는 Tower Defense Kit의 범용
    ///   나무 구조물이라 마을이라기보다 초소/비계에 가깝다), 큐브 2개(벽+지붕, 지붕은 큐브를 Z축 45도
    ///   회전시켜 만드는 저폴리 오두막 기법)로 직접 만든 오두막 3채를 크기/회전을 다르게 흩어 심는다.
    ///   기존에 쓰던 wood-structure.fbx는 오두막들 사이에 작은 창고 겸용 소품으로 재활용한다.
    /// - Ruin/Resource_Food/Resource_Ore: 단일 메시 하나로는(각각 실측 발자국 0.3x0.3, 0.27x0.25,
    ///   0.78x1.02 — MeshInspector로 실측) 타일의 절반도 못 채워서, 같은 메시를 크기/회전을 바꿔 2~3개
    ///   흩뿌린 군집으로 만든다. "유적 한 무더기"/"버섯 군락"/"바위 노두"처럼 이름에 맞는 모양도 되고
    ///   GridView.StructureLocalScale(0.85)을 곱해도 타일 발자국의 대부분을 채운다.
    /// - Starfish: Nature Kit에 적당한 기성 모델이 없어, 여기서 5각 별 모양 평면 메시를 직접 생성한다
    ///   (RuntimeSprite.CreateCircle처럼 절차적으로 만드는 선례를 3D 메시로 확장).
    ///
    /// 색은 TileAssetSetup과 같은 이유로 RuntimeMaterial.CreateColored로 타입별 단색을 입힌다(프로젝트
    /// 전체가 저폴리+단색 스타일이라 텍스처 매핑 없이도 스타일이 일관됨). Village만 벽/지붕 두 톤을 쓴다.
    /// </summary>
    public static class StructureAssetSetup
    {
        private const string CapitalMeshPath = "Assets/Art/Castle/Kenney/tower-round-build-f.fbx";
        private const string VillageAccentMeshPath = "Assets/Art/Castle/Kenney/wood-structure.fbx";
        private const string RuinMeshPath = "Assets/Art/Nature/Kenney/statue_columnDamaged.fbx";
        private const string ResourceFoodMeshPath = "Assets/Art/Nature/Kenney/mushroom_redGroup.fbx";
        private const string ResourceOreMeshPath = "Assets/Art/Nature/Kenney/rock_largeA.fbx";

        private const string PrefabFolder = "Assets/Prefabs/Structures";
        private const string MaterialFolder = "Assets/Materials";
        private const string StarMeshAssetPath = "Assets/Art/Nature/StarfishMesh.asset";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        private static readonly Color CapitalColor = new Color(0.85f, 0.7f, 0.15f);       // 금색 — 눈에 띄는 랜드마크
        private static readonly Color VillageWallColor = new Color(0.75f, 0.62f, 0.42f);  // 밝은 나무 벽
        private static readonly Color VillageRoofColor = new Color(0.62f, 0.26f, 0.16f);  // 선명한 테라코타 지붕/소품 —
        // 등각 카메라(BattleController.isoPitchDegrees=35.264도)는 지붕 윗면 위주로 보여서, 벽보다 짙기만
        // 하고 채도가 낮으면(처음 시도) 오두막들이 뭉뚱그려 어두운 덩어리로 보인다 — 선명하게 다른 색으로 뺀다.
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
            var village = GenerateVillagePrefab();
            // 서 있는 부러진 기둥 1개 + 쓰러져 옆으로 누운 기둥 2개(X축을 90도 가까이 돌려 눕힌다) —
            // 전부 세워두면 부러진 느낌 없이 오벨리스크 숲처럼 보여서(처음 시도에서 확인), "무너진 유적"답게
            // 일부는 눕혀 바닥에 흩어놓는다. 누운 기둥은 원래 세로였던 축이 눕는 만큼 y를 반지름만큼
            // 띄워야 바닥 밑으로 파고들지 않는다. 조각 사이 간격은 좁게(등각 카메라로 보면 넓게 흩어놓은
            // 조각들은 서로 이어지지 않는 점처럼 보여서 — TempStructureShot으로 확인) 한 덩어리처럼 묶는다.
            var ruin = GenerateClusterPrefab("Structure_Ruin", RuinMeshPath, RuinColor, new[]
            {
                (pos: new Vector3(-0.11f, 0f, -0.09f), euler: new Vector3(0f, 25f, 0f), scale: 0.9f),
                (pos: new Vector3(0.13f, 0.11f, 0.06f), euler: new Vector3(85f, 15f, 0f), scale: 1.05f),
                (pos: new Vector3(0.0f, 0.08f, 0.21f), euler: new Vector3(80f, -30f, 10f), scale: 0.7f),
            });
            // 버섯 2무더기를 가깝게 겹쳐 심어(원래 3개를 넓게 흩어놓았더니 등각 시점에서 점처럼 보여서
            // 줄임) 하나의 굵은 식용 버섯 군락처럼 보이게 한다.
            var resourceFood = GenerateClusterPrefab("Structure_ResourceFood", ResourceFoodMeshPath, ResourceFoodColor, new[]
            {
                (pos: new Vector3(-0.13f, 0f, -0.05f), euler: new Vector3(0f, 10f, 0f), scale: 1.7f),
                (pos: new Vector3(0.14f, 0f, 0.10f), euler: new Vector3(0f, 100f, 0f), scale: 1.6f),
            });
            var resourceOre = GenerateClusterPrefab("Structure_ResourceOre", ResourceOreMeshPath, ResourceOreColor, new[]
            {
                (pos: new Vector3(0.05f, 0f, 0f), euler: new Vector3(0f, 15f, 0f), scale: 0.95f),
                (pos: new Vector3(-0.34f, 0f, 0.28f), euler: new Vector3(0f, 50f, 0f), scale: 0.5f),
            });
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

        /// <summary>같은 메시를 layout(로컬 위치/오일러 회전/스케일)만큼 반복 배치해 흩뿌린 군집
        /// 프리팹을 만든다 — 낱개 메시 하나로는 타일 발자국을 못 채우는 Ruin/Resource_Food/Resource_Ore가
        /// 대상(StructureAssetSetup 클래스 문서 참고). Ruin은 X회전도 써서 기둥을 눕힌다.</summary>
        private static GameObject GenerateClusterPrefab(string prefabName, string meshPath, Color color, (Vector3 pos, Vector3 euler, float scale)[] layout)
        {
            var mat = GenerateOrLoadMaterial(prefabName, color);
            var root = new GameObject(prefabName);

            foreach (var piece in layout)
            {
                var child = InstantiateMeshChild(meshPath, root.transform, piece.pos, piece.euler, Vector3.one * piece.scale);
                if (child == null)
                {
                    Object.DestroyImmediate(root);
                    return null;
                }
                TintAllRenderers(child, mat);
            }

            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Village는 기성 "집" 모델이 없어 오두막 3채(CreateHut)를 직접 만들어 흩어 심고, 원래
        /// 쓰던 wood-structure.fbx는 오두막 사이 창고 소품으로 축소해 재활용한다.</summary>
        private static GameObject GenerateVillagePrefab()
        {
            var wallMat = GenerateOrLoadMaterial("Structure_Village", VillageWallColor);
            var roofMat = GenerateOrLoadMaterial("Structure_Village_Roof", VillageRoofColor);

            var root = new GameObject("Structure_Village");

            CreateHut(root.transform, new Vector3(-0.13f, 0f, -0.11f), 15f, new Vector3(0.42f, 0.28f, 0.36f), wallMat, roofMat);
            CreateHut(root.transform, new Vector3(0.16f, 0f, 0.08f), -25f, new Vector3(0.34f, 0.24f, 0.30f), wallMat, roofMat);
            CreateHut(root.transform, new Vector3(-0.03f, 0f, 0.22f), 55f, new Vector3(0.28f, 0.20f, 0.26f), wallMat, roofMat);

            var accent = InstantiateMeshChild(VillageAccentMeshPath, root.transform, new Vector3(0.10f, 0f, -0.24f), new Vector3(0f, 10f, 0f), Vector3.one * 0.32f);
            if (accent != null) TintAllRenderers(accent, roofMat);

            string path = $"{PrefabFolder}/Structure_Village.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>벽(큐브)과 지붕(큐브를 Z축 45도 돌려 삼각 단면으로 만드는 흔한 저폴리 오두막 기법 —
        /// 지붕 피벗을 벽 꼭대기에 두면 회전된 정사각형의 대각선 폭 때문에 처마가 벽 밖으로 살짝
        /// 튀어나온다) 두 개로 이루어진 오두막을 parent 아래 만든다.</summary>
        private static void CreateHut(Transform parent, Vector3 localPos, float rotationY, Vector3 wallSize, Material wallMat, Material roofMat)
        {
            var hut = new GameObject("Hut");
            hut.transform.SetParent(parent, false);
            hut.transform.localPosition = localPos;
            hut.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.transform.SetParent(hut.transform, false);
            wall.transform.localScale = wallSize;
            wall.transform.localPosition = new Vector3(0f, wallSize.y * 0.5f, 0f);
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.transform.SetParent(hut.transform, false);
            float roofSpan = wallSize.x * 0.78f;
            roof.transform.localScale = new Vector3(roofSpan, roofSpan, wallSize.z * 1.08f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.transform.localPosition = new Vector3(0f, wallSize.y, 0f);
            roof.GetComponent<Renderer>().sharedMaterial = roofMat;
        }

        /// <summary>meshPath의 프리팹을 parent 아래 인스턴스화하고 로컬 위치/오일러 회전/스케일을
        /// 지정한다. GenerateClusterPrefab/GenerateVillagePrefab의 소품 배치에 공용으로 쓰인다.</summary>
        private static GameObject InstantiateMeshChild(string meshPath, Transform parent, Vector3 localPos, Vector3 euler, Vector3 localScale)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (mesh == null)
            {
                Debug.LogError($"[StructureAssetSetup] mesh not found: {meshPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(mesh, parent);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.Euler(euler);
            instance.transform.localScale = localScale;
            return instance;
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
