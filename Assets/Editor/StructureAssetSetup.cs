using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 타일 위 구조물(수도/마을/유적/자원/등대/불가사리) 프리팹을 만들어주는 1회성 배치 도구. TileAssetSetup과
    /// 같은 이유로 Unity CLI(-executeMethod)로만 실행한다(에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.StructureAssetSetup.GenerateAll
    ///
    /// 메시 출처(전부 CC0, Kenney):
    /// - Capital: Assets/Art/Castle/Kenney/tower-round-build-f.fbx 하나 — 실측 발자국이 이미 1x1 타일
    ///   전체를 채워서(bounds size=(1,1.79,1)) 그대로 랜드마크로 쓴다.
    /// - Village: 기존엔 Castle Kenney 팩의 wood-structure.fbx(범용 목재 구조물이라 마을보다 초소에
    ///   가까움)나 직접 만든 큐브 오두막을 썼는데 둘 다 "마을"로 안 읽혀서, Kenney의 모듈형 Fantasy Town
    ///   Kit(Assets/Art/Village/Kenney, CC0)을 새로 받아 벽/문/창문/지붕 조각으로 오두막 2채(CreateCottage)를
    ///   조립하고 장터 좌판(stall.fbx)을 곁들인다. 벽 조각은 1x1x1 셀의 -X면에 놓이도록 만들어져 있어서
    ///   (MeshInspector로 실측: wall.fbx bounds size=(0.1,1,1)) Y회전 0/90/180/270으로 4면을 두르면 저절로
    ///   상자가 닫힌다. roof-gable-end.fbx는 처마 삼각벽까지 포함된 완결형 1칸 지붕이라(roof-gable.fbx와
    ///   크기가 거의 같음 — 이어붙이는 중간 조각이 아니다) 한 채당 하나만 얹으면 된다.
    /// - Ruin/Resource_Food/Resource_Ore: 단일 메시 하나로는(각각 실측 발자국 0.3x0.3, 0.27x0.25,
    ///   0.78x1.02 — MeshInspector로 실측) 타일의 절반도 못 채워서, 같은 메시를 크기/회전을 바꿔 2~3개
    ///   흩뿌린 군집으로 만든다. "유적 한 무더기"/"버섯 군락"/"바위 노두"처럼 이름에 맞는 모양도 되고
    ///   GridView.StructureLocalScale(0.85)을 곱해도 타일 발자국의 대부분을 채운다.
    /// - Starfish: Nature Kit에 적당한 기성 모델이 없어, 여기서 5각 별 모양 평면 메시를 직접 생성한다
    ///   (RuntimeSprite.CreateCircle처럼 절차적으로 만드는 선례를 3D 메시로 확장).
    /// - Resource_Fruit/Crop/Animal/Fish, Lighthouse(9차 재정비 자원 + 등대): 가져온 Kenney 메시 중 맞는 게
    ///   없어서 ProceduralPropMeshes의 단위 저폴리 조각(상자/원기둥/원뿔/구)을 조립한다 — 과일 덤불(초록 덤불 +
    ///   빨간 열매), 밀밭(흙 + 황금 이삭 20포기), 사슴(몸통/다리/목/머리/뿔), 물고기 세 마리(몸통 + 꼬리지느러미 +
    ///   물결 고리), 등대(바위 받침 + 가늘어지는 흰 탑 + 빨간 띠 + 등불 + 지붕). Resource_Metal은 광물 노두
    ///   (Structure_ResourceOre)를 그대로 쓴다. GenerateProceduralStructures만 따로 실행해도 된다.
    ///
    /// 색은 TileAssetSetup과 같은 이유로 RuntimeMaterial.CreateColored로 타입별 단색을 입힌다(프로젝트
    /// 전체가 저폴리+단색 스타일이라 텍스처 매핑 없이도 스타일이 일관됨). Village만 벽/지붕 두 톤을 쓴다.
    /// </summary>
    public static class StructureAssetSetup
    {
        private const string CapitalMeshPath = "Assets/Art/Castle/Kenney/tower-round-build-f.fbx";
        private const string VillageWallMeshPath = "Assets/Art/Village/Kenney/wall.fbx";
        private const string VillageWallDoorMeshPath = "Assets/Art/Village/Kenney/wall-door.fbx";
        private const string VillageWallWindowMeshPath = "Assets/Art/Village/Kenney/wall-window-shutters.fbx";
        private const string VillageRoofMeshPath = "Assets/Art/Village/Kenney/roof-gable-end.fbx";
        private const string VillageChimneyMeshPath = "Assets/Art/Village/Kenney/chimney.fbx";
        private const string VillageStallMeshPath = "Assets/Art/Village/Kenney/stall.fbx";
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
        private static readonly Color BushColor = new Color(0.22f, 0.5f, 0.22f);          // 과일 덤불(숲 타일보다 밝게)
        private static readonly Color FruitColor = new Color(0.9f, 0.18f, 0.2f);          // 빨간 열매
        private static readonly Color SoilColor = new Color(0.45f, 0.3f, 0.18f);          // 밭 흙
        private static readonly Color WheatColor = new Color(0.93f, 0.78f, 0.3f);         // 황금 이삭
        private static readonly Color DeerColor = new Color(0.58f, 0.36f, 0.18f);         // 사슴 몸통
        private static readonly Color AntlerColor = new Color(0.92f, 0.87f, 0.74f);       // 뿔/꼬리
        private static readonly Color FishColor = new Color(1f, 0.72f, 0.25f);            // 물 위에서 잘 보이는 금빛 물고기
        private static readonly Color FinColor = new Color(0.95f, 0.5f, 0.18f);           // 꼬리지느러미
        private static readonly Color RippleColor = new Color(0.78f, 0.9f, 1f);           // 물결
        private static readonly Color LighthouseColor = new Color(0.96f, 0.96f, 0.93f);   // 흰 탑
        private static readonly Color LighthouseBandColor = new Color(0.82f, 0.16f, 0.13f); // 빨간 띠/지붕
        private static readonly Color LampColor = new Color(1f, 0.9f, 0.4f);              // 등불
        private static readonly Color BaseRockColor = new Color(0.52f, 0.52f, 0.55f);     // 받침 바위

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

            GenerateProceduralStructures();
            Debug.Log("[StructureAssetSetup] GenerateAll done");
        }

        /// <summary>기성 메시가 없는 구조물 5종(과일/작물/사냥감/물고기/등대)을 ProceduralPropMeshes 조각으로
        /// 조립해 프리팹으로 저장하고 Sandbox 씬의 BattleController에 배정한다. 기존 6종 프리팹은 건드리지 않는다.
        /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.StructureAssetSetup.GenerateProceduralStructures
        /// 좌표는 프리팹 원점 = 타일 윗면 중심, 타일 한 칸 = 1(GridView가 0.85배로 줄여 얹는다).</summary>
        public static void GenerateProceduralStructures()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Structures");
            EnsureFolder("Assets", "Materials");

            var fruit = SavePrefab(BuildFruitBush());
            var crop = SavePrefab(BuildWheatField());
            var animal = SavePrefab(BuildDeer());
            var fish = SavePrefab(BuildFishSchool());
            var lighthouse = SavePrefab(BuildLighthouse());
            // 불가사리도 절차적 메시라 같이 다시 굽는다(별 메시 감김 방향 수정 반영 — 같은 경로라 씬 참조는 유지).
            GenerateStarfishPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fruit != null && crop != null && animal != null && fish != null && lighthouse != null)
                AssignProceduralToSandboxScene(fruit, crop, animal, fish, lighthouse);
            Debug.Log("[StructureAssetSetup] GenerateProceduralStructures done");
        }

        private static GameObject SavePrefab(GameObject root)
        {
            string path = $"{PrefabFolder}/{root.name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject AddPart(Transform parent, Mesh mesh, Material material, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>과일: 크기가 다른 초록 덤불 3개를 겹쳐 심고, 각 덤불 윗반구 표면에 빨간 열매를 박는다.</summary>
        private static GameObject BuildFruitBush()
        {
            var root = new GameObject("Structure_ResourceFruit");
            var sphere = ProceduralPropMeshes.Sphere();
            var bushMat = GenerateOrLoadMaterial("Structure_ResourceFruit_Bush", BushColor);
            var fruitMat = GenerateOrLoadMaterial("Structure_ResourceFruit", FruitColor);

            var bushes = new[]
            {
                (center: new Vector3(-0.12f, 0.17f, -0.05f), radius: new Vector3(0.26f, 0.2f, 0.26f), fruits: 5),
                (center: new Vector3(0.17f, 0.14f, 0.13f), radius: new Vector3(0.21f, 0.16f, 0.21f), fruits: 4),
                (center: new Vector3(0.1f, 0.11f, -0.24f), radius: new Vector3(0.16f, 0.12f, 0.16f), fruits: 3),
            };
            foreach (var bush in bushes)
            {
                AddPart(root.transform, sphere, bushMat, bush.center, Vector3.zero, bush.radius);
                for (int i = 0; i < bush.fruits; i++)
                {
                    // 방위각을 고르게 돌리고 높이각은 번갈아 — 윗반구 표면에 흩어 박는다.
                    float azimuth = (i * 360f / bush.fruits + 20f) * Mathf.Deg2Rad;
                    float elevation = (i % 2 == 0 ? 30f : 55f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));
                    var surface = bush.center + Vector3.Scale(dir, bush.radius) * 0.95f;
                    AddPart(root.transform, sphere, fruitMat, surface, Vector3.zero, Vector3.one * 0.05f);
                }
            }
            return root;
        }

        /// <summary>작물: 흙 두둑 위에 4줄 x 5포기 황금 이삭(가는 원뿔)을 심는다 — 위치/기울기를 고정 패턴으로
        /// 살짝씩 흐트러뜨려 기계적으로 줄 선 느낌을 줄인다.</summary>
        private static GameObject BuildWheatField()
        {
            var root = new GameObject("Structure_ResourceCrop");
            var soilMat = GenerateOrLoadMaterial("Structure_ResourceCrop_Soil", SoilColor);
            var wheatMat = GenerateOrLoadMaterial("Structure_ResourceCrop", WheatColor);

            AddPart(root.transform, ProceduralPropMeshes.Box(), soilMat, new Vector3(0f, 0.015f, 0f), new Vector3(0f, 12f, 0f), new Vector3(0.7f, 0.03f, 0.56f));
            var cone = ProceduralPropMeshes.Cone();
            var rotation = Quaternion.Euler(0f, 12f, 0f);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 5; col++)
                {
                    float jitterX = ((row * 7 + col * 3) % 5 - 2) * 0.008f;
                    float jitterZ = ((row * 3 + col * 5) % 5 - 2) * 0.008f;
                    var pos = rotation * new Vector3(-0.26f + col * 0.13f + jitterX, 0.03f, -0.19f + row * 0.127f + jitterZ);
                    float height = 0.24f + ((row + col) % 3) * 0.03f;
                    var tilt = new Vector3(((row + col * 2) % 3 - 1) * 6f, 0f, ((row * 2 + col) % 3 - 1) * 6f);
                    AddPart(root.transform, cone, wheatMat, pos, tilt, new Vector3(0.035f, height, 0.035f));
                }
            return root;
        }

        /// <summary>사냥감: 상자 몸통/다리/목/머리/꼬리 + 원뿔 뿔로 조립한 사슴 한 마리(등각 시점에서 옆모습이
        /// 보이게 살짝 돌려 세운다). 머리는 +X 방향.</summary>
        private static GameObject BuildDeer()
        {
            var root = new GameObject("Structure_ResourceAnimal");
            var deer = new GameObject("Deer").transform;
            deer.SetParent(root.transform, false);
            deer.localRotation = Quaternion.Euler(0f, 45f, 0f); // 머리(+X)가 화면 오른쪽 아래를 향해 등각 카메라에 옆모습이 보이게

            var box = ProceduralPropMeshes.Box();
            var cone = ProceduralPropMeshes.Cone();
            var bodyMat = GenerateOrLoadMaterial("Structure_ResourceAnimal", DeerColor);
            var antlerMat = GenerateOrLoadMaterial("Structure_ResourceAnimal_Antler", AntlerColor);

            AddPart(deer, box, bodyMat, new Vector3(0f, 0.31f, 0f), Vector3.zero, new Vector3(0.42f, 0.17f, 0.15f));
            foreach (var (x, z) in new[] { (0.15f, 0.05f), (0.15f, -0.05f), (-0.15f, 0.05f), (-0.15f, -0.05f) })
                AddPart(deer, box, bodyMat, new Vector3(x, 0.12f, z), Vector3.zero, new Vector3(0.045f, 0.24f, 0.045f));
            AddPart(deer, box, bodyMat, new Vector3(0.21f, 0.43f, 0f), new Vector3(0f, 0f, -28f), new Vector3(0.075f, 0.2f, 0.075f));
            AddPart(deer, box, bodyMat, new Vector3(0.29f, 0.53f, 0f), new Vector3(0f, 0f, -10f), new Vector3(0.15f, 0.085f, 0.085f));
            AddPart(deer, box, antlerMat, new Vector3(-0.225f, 0.36f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0.04f, 0.07f, 0.05f));
            foreach (float side in new[] { 1f, -1f })
            {
                AddPart(deer, cone, antlerMat, new Vector3(0.26f, 0.57f, 0.03f * side), new Vector3(25f * side, 0f, 18f), new Vector3(0.018f, 0.16f, 0.018f));
                AddPart(deer, cone, antlerMat, new Vector3(0.28f, 0.64f, 0.06f * side), new Vector3(55f * side, 0f, -10f), new Vector3(0.012f, 0.08f, 0.012f));
            }
            return root;
        }

        /// <summary>물고기: 납작한 타원체 몸통 + 원뿔을 눕혀 만든 부채꼴 꼬리지느러미 + 등지느러미 세 마리를 물
        /// 위에 띄우고, 가운데에 옅은 물결 고리를 깐다. 한 마리는 튀어 오르는 자세로 기울인다.</summary>
        private static GameObject BuildFishSchool()
        {
            var root = new GameObject("Structure_ResourceFish");
            var sphere = ProceduralPropMeshes.Sphere();
            var cone = ProceduralPropMeshes.Cone();
            var bodyMat = GenerateOrLoadMaterial("Structure_ResourceFish", FishColor);
            var finMat = GenerateOrLoadMaterial("Structure_ResourceFish_Fin", FinColor);
            var rippleMat = GenerateOrLoadMaterial("Structure_ResourceFish_Ripple", RippleColor);

            AddPart(root.transform, ProceduralPropMeshes.Cylinder(), rippleMat, Vector3.zero, Vector3.zero, new Vector3(0.38f, 0.01f, 0.38f));

            var fishes = new[]
            {
                (pos: new Vector3(-0.16f, 0.09f, 0.14f), yaw: 20f, pitch: 0f, size: 1.45f),
                (pos: new Vector3(0.2f, 0.09f, -0.02f), yaw: 150f, pitch: 0f, size: 1.25f),
                (pos: new Vector3(-0.04f, 0.26f, -0.2f), yaw: -60f, pitch: 30f, size: 1.3f),
            };
            foreach (var f in fishes)
            {
                var fish = new GameObject("Fish").transform;
                fish.SetParent(root.transform, false);
                fish.localPosition = f.pos;
                fish.localRotation = Quaternion.Euler(0f, f.yaw, f.pitch);
                fish.localScale = Vector3.one * f.size;
                AddPart(fish, sphere, bodyMat, Vector3.zero, Vector3.zero, new Vector3(0.14f, 0.06f, 0.055f));
                // 원뿔을 Z축 -90도로 눕히면 꼭짓점이 +X(몸통 쪽)를 향하고 바닥 부채꼴이 뒤로 펼쳐진다. 로컬 X(반지름)가
                // 세로 폭, 로컬 Z가 두께라 얇은 세로 지느러미가 된다.
                AddPart(fish, cone, finMat, new Vector3(-0.22f, 0f, 0f), new Vector3(0f, 0f, -90f), new Vector3(0.065f, 0.1f, 0.012f));
                AddPart(fish, cone, finMat, new Vector3(-0.01f, 0.04f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0.035f, 0.05f, 0.008f));
            }
            return root;
        }

        /// <summary>등대: 바위 받침 위에 위로 갈수록 가늘어지는 흰 탑, 빨간 띠 두 줄, 난간, 노란 등불, 빨간 지붕.</summary>
        private static GameObject BuildLighthouse()
        {
            var root = new GameObject("Structure_Lighthouse");
            var sphere = ProceduralPropMeshes.Sphere();
            var cylinder = ProceduralPropMeshes.Cylinder();
            var cone = ProceduralPropMeshes.Cone();
            var towerMat = GenerateOrLoadMaterial("Structure_Lighthouse", LighthouseColor);
            var bandMat = GenerateOrLoadMaterial("Structure_Lighthouse_Band", LighthouseBandColor);
            var lampMat = GenerateOrLoadMaterial("Structure_Lighthouse_Lamp", LampColor);
            var rockMat = GenerateOrLoadMaterial("Structure_Lighthouse_Rock", BaseRockColor);

            AddPart(root.transform, sphere, rockMat, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.32f, 0.09f, 0.3f));
            AddPart(root.transform, sphere, rockMat, new Vector3(0.2f, 0.02f, -0.14f), new Vector3(0f, 30f, 0f), new Vector3(0.13f, 0.07f, 0.11f));
            // 탑: 바닥 반지름 0.15, 윗면 0.105(TaperedCylinder 70%), y 0.05~0.65.
            AddPart(root.transform, ProceduralPropMeshes.TaperedCylinder(), towerMat, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.15f, 0.6f, 0.15f));
            // 띠: 그 높이의 탑 반지름(y 0.2 -> 0.139, y 0.45 -> 0.12)보다 살짝 크게.
            AddPart(root.transform, cylinder, bandMat, new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.142f, 0.07f, 0.142f));
            AddPart(root.transform, cylinder, bandMat, new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(0.124f, 0.07f, 0.124f));
            AddPart(root.transform, cylinder, rockMat, new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(0.14f, 0.025f, 0.14f));
            AddPart(root.transform, cylinder, lampMat, new Vector3(0f, 0.675f, 0f), Vector3.zero, new Vector3(0.075f, 0.1f, 0.075f));
            AddPart(root.transform, cone, bandMat, new Vector3(0f, 0.775f, 0f), Vector3.zero, new Vector3(0.11f, 0.13f, 0.11f));
            return root;
        }

        /// <summary>GenerateProceduralStructures가 만든 5종을 Sandbox 씬 BattleController에 배정한다.</summary>
        private static void AssignProceduralToSandboxScene(GameObject fruit, GameObject crop, GameObject animal, GameObject fish, GameObject lighthouse)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) == null) return;

            var scene = EditorSceneManager.OpenScene(SandboxScenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[StructureAssetSetup] BattleController not found in " + SandboxScenePath);
                return;
            }

            SetPrivateField(controller, "resourceFruitStructurePrefab", fruit);
            SetPrivateField(controller, "resourceCropStructurePrefab", crop);
            SetPrivateField(controller, "resourceAnimalStructurePrefab", animal);
            SetPrivateField(controller, "resourceFishStructurePrefab", fish);
            SetPrivateField(controller, "lighthouseStructurePrefab", lighthouse);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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

        /// <summary>Village는 Fantasy Town Kit 벽/지붕 조각으로 오두막 2채(CreateCottage)를 조립해
        /// 흩어 심고, 장터 좌판(stall.fbx)을 하나 곁들인다.</summary>
        private static GameObject GenerateVillagePrefab()
        {
            var wallMat = GenerateOrLoadMaterial("Structure_Village", VillageWallColor);
            var roofMat = GenerateOrLoadMaterial("Structure_Village_Roof", VillageRoofColor);

            var root = new GameObject("Structure_Village");

            CreateCottage(root.transform, new Vector3(-0.20f, 0f, -0.15f), 15f, 0.48f, wallMat, roofMat, withChimney: true);
            CreateCottage(root.transform, new Vector3(0.20f, 0f, 0.12f), -30f, 0.42f, wallMat, roofMat, withChimney: false);

            var stall = InstantiateMeshChild(VillageStallMeshPath, root.transform, new Vector3(-0.05f, 0f, 0.32f), new Vector3(0f, 100f, 0f), Vector3.one * 0.4f);
            if (stall != null) TintAllRenderers(stall, roofMat);

            string path = $"{PrefabFolder}/Structure_Village.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>벽 4장(문 1 + 창문 2 + 막힌 벽 1, Y회전 0/90/180/270으로 1x1x1 셀을 두름) + 지붕
        /// 1개(+선택적으로 굴뚝)로 오두막 하나를 조립한다. 벽 조각의 원점이 셀 중심이라(-X면에 걸침)
        /// 그대로 4번 회전시키면 상자가 저절로 닫힌다 — StructureAssetSetup 클래스 문서의 실측값 참고.</summary>
        private static void CreateCottage(Transform parent, Vector3 localPos, float rotationY, float scale, Material wallMat, Material roofMat, bool withChimney)
        {
            var cottage = new GameObject("Cottage");
            cottage.transform.SetParent(parent, false);
            cottage.transform.localPosition = localPos;
            cottage.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            cottage.transform.localScale = Vector3.one * scale;

            TintAllRenderers(InstantiateMeshChild(VillageWallDoorMeshPath, cottage.transform, Vector3.zero, new Vector3(0f, 0f, 0f), Vector3.one), wallMat);
            TintAllRenderers(InstantiateMeshChild(VillageWallMeshPath, cottage.transform, Vector3.zero, new Vector3(0f, 180f, 0f), Vector3.one), wallMat);
            TintAllRenderers(InstantiateMeshChild(VillageWallWindowMeshPath, cottage.transform, Vector3.zero, new Vector3(0f, 90f, 0f), Vector3.one), wallMat);
            TintAllRenderers(InstantiateMeshChild(VillageWallWindowMeshPath, cottage.transform, Vector3.zero, new Vector3(0f, 270f, 0f), Vector3.one), wallMat);
            TintAllRenderers(InstantiateMeshChild(VillageRoofMeshPath, cottage.transform, new Vector3(0f, 1f, 0f), Vector3.zero, Vector3.one), roofMat);

            if (withChimney)
                TintAllRenderers(InstantiateMeshChild(VillageChimneyMeshPath, cottage.transform, new Vector3(0.3f, 1f, -0.3f), Vector3.zero, Vector3.one), roofMat);
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
                triangles[i * 3 + 1] = next + 1; // 위에서 봤을 때 시계 방향이어야 윗면이 앞면(예전엔 반대라 뒷면 컬링으로 안 보였다)
                triangles[i * 3 + 2] = i + 1;
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
            if (root == null) return;
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
