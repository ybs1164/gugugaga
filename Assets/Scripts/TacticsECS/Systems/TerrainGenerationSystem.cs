using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld를 바이옴 CSV 규칙에 따라 절차적으로 채우는 상태 없는 정적 시스템(규칙 3) — 자체 필드를
    /// 갖지 않고, 매번 GridWorld/BiomeCsvRow와 시드를 인자로 받아 결과를 GridWorld에 직접 써넣는다.
    /// docs/PolytopiaMapGeneration.md 기준 2차 재정비: 완전 랜덤 Voronoi 시드 대신 쿼드런트(구역) 기반
    /// 앵커 배치(Polytopia의 수도 배치), 앵커 기준 Inner/Outer 이중 확률, 일반화된 거리 제약(MinDistance/
    /// EdgeMargin) + 맵 크기 비례 개수(CountPerTiles)를 반영했다. 3차 재정비: 습도 배율 지원(2절) +
    /// 계산된 앵커를 StructureGenerationSystem이 재사용할 수 있도록 반환.
    /// Sandbox/BattleController.HandleGenerateTerrain이 호출한다.
    /// </summary>
    public static class TerrainGenerationSystem
    {
        /// <summary>이미 놓인 이웃 타입이 없을 때도 완전히 0이 되지 않도록 노이즈 가중치에 더하는 바닥값.</summary>
        private const float NoiseWeightFloor = 0.25f;

        /// <summary>숲/산 레이어(docs/PolytopiaMapGeneration.md 12.1절)가 쓰는 TileTypeId. TerrainType은 Land 그대로다.</summary>
        public const string ForestTileId = "Forest";
        public const string MountainTileId = "Mountain";

        /// <summary>깊은 바다 TileTypeId(9차 재정비 — 원문 3/9절의 얕은 물/깊은 바다 구분). 육지와 8방향으로
        /// 맞닿지 않은 물 칸이 이 타일이 되고(ClassifyWaterDepth), 육지와 맞닿은 물 칸은 바이옴 CSV의 물
        /// 타일(얕은 물) 그대로 남는다. TerrainType은 둘 다 Water.</summary>
        public const string OceanTileId = "Ocean";

        /// <summary>Polytopia 기준 스폰 비율(3절, Luxidoor 기준값) — 육지 중 산 14%, 숲 38%, 나머지 평지.</summary>
        private const float BaseMountainFraction = 0.14f;
        private const float BaseForestFraction = 0.38f;

        /// <summary>수도는 맵 가장자리로부터 최소 이 거리(12절 보강 3 — "수도가 벽에 붙는 문제").</summary>
        public const int CapitalEdgeMargin = 2;
        /// <summary>쿼드런트 맵에서 수도 주변 이 반경(체비쇼프)까지 육지로 강제 — 1이면 3x3(12절 보강 2).</summary>
        public const int CapitalLandRadius = 1;
        /// <summary>Pangea/Continents에서 수도가 설 수 있는 육지 덩어리(4방향 연결)의 최소 크기(12절 보강 2).</summary>
        public const int MinCapitalLandmassSize = 9;
        /// <summary>수도끼리 최소 거리(체비쇼프) — 쿼드런트 중심부 샘플링과 함께 간격을 고르게 한다(12절 보강 1).</summary>
        private const int CapitalMinDistance = 3;
        /// <summary>수도/마을(모든 "도시")끼리 최소 거리(체비쇼프) — 2면 바로 인접(대각선 포함) 금지(12절 보강 4).
        /// 원문 7.3절 "Pre-terrain 마을은 다른 마을로부터 2칸"(Suburb 포함)의 값이고, 모든 도시가 지키는 하한이다.</summary>
        public const int CityMinDistance = 2;
        /// <summary>Post-terrain 마을(외딴 섬 마을, Pangea/Continents 본토 마을 포함)의 도시 간 최소 거리 —
        /// 원문 7.4절 "다른 마을로부터 2칸 이내에 놓이면 안 됨" = 거리 3 이상. CSV MinDistance가 더 크면 그 값.</summary>
        public const int PostTerrainCityMinDistance = 3;
        /// <summary>Post-terrain 마을의 가장자리 여백(원문 7.4절 "맵 가장자리로부터 2칸 이내 금지").</summary>
        public const int PostTerrainVillageEdgeMargin = 2;
        /// <summary>Suburb가 수도로부터 떨어질 수 있는 최대 거리(체비쇼프).</summary>
        private const int SuburbRadius = 3;
        /// <summary>수도마다 시도하는 Suburb 수 — 원문 "최대 2개, 보통 2개". 자리가 없을 때만 0~1개가 된다.</summary>
        private const int SuburbsPerCapital = 2;
        /// <summary>Continents 대륙 한 덩어리의 크기 범위(원문 7.5절 "30~200타일").</summary>
        private const int ContinentMinSize = 30;
        private const int ContinentMaxSize = 200;
        /// <summary>Pangea/Continents 수도 선택 시도 횟수 — 가장 고르게 퍼진(최소 쌍 거리가 가장 큰) 조합을 쓴다.</summary>
        private const int CapitalSelectionTrials = 12;

        /// <summary>docs/PolytopiaMapGeneration.md 2절의 6종 맵 타입 중, 개별 타일 확률(+MinDistance)에
        /// 맡기지 않고 전용 "랜드마스 마스크"로 모양 자체를 만드는 타입들. Drylands는 원래도 물이 거의
        /// 없어(목표 0~10%) 기존 방식으로 충분해 여기 포함하지 않는다 — 마스크가 필요 없다.</summary>
        public enum MapShapeMode { Freeform, Pangea, Lakes, Continents, Archipelago, Waterworld }

        /// <summary>지형을 생성하고, 바이옴별(수도) 앵커를 반환한다 — Suburb/Pre-terrain 마을 위치까지
        /// 필요 없는 대부분의 호출부(검증 스크립트 등)를 위한 얇은 오버로드. 내부적으로 전체 오버로드에
        /// 위임하고 마을 위치는 버린다.</summary>
        public static Vector2Int[] Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int seed, float wetnessMultiplier = 1f,
            MapShapeMode shapeMode = MapShapeMode.Freeform, float targetWaterFraction = 0.5f)
            => Generate(grid, biomes, seed, wetnessMultiplier, shapeMode, targetWaterFraction, out _, out _);

        /// <summary>지형을 생성하고, 수도 앵커 + Suburb/사전 확정 마을 위치를 반환한다(7차 재정비 —
        /// docs/PolytopiaMapGeneration.md 5절 순서대로 "수도 -> 마을 -> 지형" 순으로 위치를 먼저 확정한
        /// 뒤 지형을 채운다). 파이프라인:
        /// 1. 쿼드런트 맵이면 수도를 먼저 뽑는다(GenerateQuadrantAnchors).
        /// 2. Suburb/Pre-terrain 마을 위치를 지형 없이 먼저 정한다(PlanPreTerrainVillages) — 맵 타입별로
        ///    있을 수도/없을 수도 있다(7.1절 매트릭스).
        /// 3. 수도+Suburb+Pre-terrain 마을 위치 전부를 "반드시 육지가 되어야 할 칸"(guaranteedLand)으로
        ///    묶어, 지형 생성이 이 칸들을 육지로 보장하도록 한다 — shapeMode가 랜드마스 마스크 경로면
        ///    마스크 점수에 보너스를 줘서(GenerateMapShapeLandMask), Freeform이면 채우기 완료 후 강제로
        ///    덮어써서(ForceLandAt) 보장한다.
        /// 4. Pangea/Continents는 땅을 먼저 만든 뒤, 원문 7.5절대로 본토에 마을을 포화 배치하고 그중 일부를
        ///    수도로 전환한다 — 수도가 되지 않은 본토 마을은 plannedVillagePositions로 반환된다.
        /// 5. 마지막에 물 칸을 얕은 물/깊은 바다(Ocean)로 분류한다(ClassifyWaterDepth).
        /// plannedVillagePositions = Lakes/Archipelago/Waterworld의 Pre-terrain 마을, 또는 Pangea/Continents의
        /// 본토 마을(둘 다 "타일을 채우기 전에 위치가 확정된 마을"). wetnessMultiplier는 Freeform 경로에서만
        /// 쓰이고(습도 프리셋 배율), 마스크 경로에서는 targetWaterFraction이 대신 목표 물 비율을 정한다.</summary>
        public static Vector2Int[] Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int seed, float wetnessMultiplier,
            MapShapeMode shapeMode, float targetWaterFraction, out Vector2Int[] suburbPositions, out Vector2Int[] plannedVillagePositions)
        {
            if (grid == null || biomes == null || biomes.Count == 0)
            {
                suburbPositions = Array.Empty<Vector2Int>();
                plannedVillagePositions = Array.Empty<Vector2Int>();
                return Array.Empty<Vector2Int>();
            }

            var rng = new Random(seed);
            // Pangea/Continents는 쿼드런트를 쓰지 않는다(7.1/7.5절) — 땅을 먼저 만든 뒤 GenerateWithShape가
            // SelectCapitalsOnLand로 수도를 고른다. 그 외는 지형보다 먼저 쿼드런트로 수도를 정한다.
            bool capitalsAfterLand = shapeMode == MapShapeMode.Pangea || shapeMode == MapShapeMode.Continents;
            var capitalAnchors = capitalsAfterLand ? Array.Empty<Vector2Int>() : GenerateQuadrantAnchors(grid, biomes.Count, rng);
            var (suburbs, preTerrain) = PlanPreTerrainVillages(grid, shapeMode, capitalAnchors, rng);

            (Vector2Int[] Anchors, Vector2Int[] Suburbs, Vector2Int[] PreTerrain) result = shapeMode != MapShapeMode.Freeform
                ? GenerateWithShape(grid, biomes, rng, shapeMode, targetWaterFraction, capitalAnchors, suburbs, preTerrain)
                : GenerateFreeform(grid, biomes, rng, wetnessMultiplier, capitalAnchors, suburbs, preTerrain);

            ClassifyWaterDepth(grid, biomes, result.Anchors);

            suburbPositions = result.Suburbs;
            plannedVillagePositions = result.PreTerrain;
            return result.Anchors;
        }

        /// <summary>물 칸을 얕은 물/깊은 바다로 나눈다(원문 3/9/10절 — 물고기는 얕은 물, 유적은 깊은 바다).
        /// 8방향 이웃 중 육지가 하나라도 있으면 얕은 물(그 칸 바이옴의 첫 물 타일 Id), 없으면 OceanTileId.
        /// 지형이 바뀐 뒤(외딴 섬 마을/Lakes 육지 다리 등) 다시 불러도 되도록 양방향으로 갱신한다 —
        /// StructureGenerationSystem도 재사용한다. 유닛이 점유한 칸은 건드리지 않는다.</summary>
        public static void ClassifyWaterDepth(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, Vector2Int[] anchors)
        {
            if (grid == null || biomes == null || biomes.Count == 0) return;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos) || grid.GetTerrain(pos) != TerrainType.Water) continue;

                    bool nearLand = false;
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                        if (grid.GetTerrain(n) == TerrainType.Land) { nearLand = true; break; }

                    string current = grid.GetTileType(pos);
                    if (!nearLand)
                    {
                        if (current != OceanTileId) grid.SetTileType(pos, OceanTileId);
                    }
                    else if (current == OceanTileId || string.IsNullOrEmpty(current))
                    {
                        int biomeIdx = anchors == null || anchors.Length == 0 ? 0 : ProceduralGenerationUtil.NearestAnchorIndex(anchors, pos);
                        string shallowId = FindFirstWaterTileId(biomes[Mathf.Clamp(biomeIdx, 0, biomes.Count - 1)]);
                        if (shallowId != null) grid.SetTileType(pos, shallowId);
                    }
                }
        }

        /// <summary>자유 배치 경로(Drylands 포함 shapeMode==Freeform). 기존 쿼터 배치 + 나머지 채우기
        /// 알고리즘은 그대로 두고, 마지막에 guaranteedLand(수도+Suburb+Pre-terrain 마을) 칸을 강제로
        /// 육지로 덮어쓰는 단계만 추가한다(ForceLandAt) — 마스크가 없는 경로라 사전에 육지를 보장할
        /// 방법이 없으므로 사후에 강제한다.</summary>
        private static (Vector2Int[] Anchors, Vector2Int[] Suburbs, Vector2Int[] PreTerrain) GenerateFreeform(GridWorld grid,
            IReadOnlyList<BiomeCsvRow> biomes, Random rng, float wetnessMultiplier, Vector2Int[] capitalAnchors, Vector2Int[] suburbs, Vector2Int[] preTerrain)
        {
            var effectiveBiomes = Mathf.Approximately(wetnessMultiplier, 1f) ? biomes : ApplyWetness(biomes, wetnessMultiplier);
            var biomeIndexPerCell = ComputeBiomeIndexPerCell(grid, capitalAnchors);
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, effectiveBiomes.Count);

            ClearGeneratedTiles(grid);
            var placedPositionsByType = BuildInitialPlacedPositions(grid);

            var cities = Combine(capitalAnchors, suburbs, preTerrain);
            PlaceMinCountQuota(grid, effectiveBiomes, biomeIndexPerCell, regionSizePerBiome, placedPositionsByType, rng);
            FillRemaining(grid, effectiveBiomes, biomeIndexPerCell, cities, placedPositionsByType, rng);

            ForceLandAt(grid, biomes, biomeIndexPerCell, ExpandToSquare(grid, capitalAnchors, CapitalLandRadius), placedPositionsByType);
            ForceLandAt(grid, biomes, biomeIndexPerCell, suburbs, placedPositionsByType);
            ForceLandAt(grid, biomes, biomeIndexPerCell, preTerrain, placedPositionsByType);

            ApplyForestAndMountains(grid, biomes, biomeIndexPerCell, cities, rng);
            return (capitalAnchors, suburbs, preTerrain);
        }

        /// <summary>맵 타입별 랜드마스 마스크 파라미터. 전부 같은 "방사형 감쇠 + 노이즈 점수 -> 순위 컷"
        /// 마스크 하나를 재사용하고, 타입마다 파라미터만 바꾼다(Pangea 전용 코드를 그대로 일반화한 것).</summary>
        private readonly struct MapShapeParams
        {
            /// <summary>랜드마스 중심점 개수. 0=순수 노이즈(중심 없음, Lakes), 1=맵 정중앙 한 점(Pangea),
            /// 2 이상=쿼드런트로 흩어진 여러 중심점(Continents/Archipelago/Waterworld — 바이옴마다 자기
            /// 땅덩이를 갖는 효과).</summary>
            public readonly int CenterCount;
            /// <summary>방사형 감쇠와 노이즈의 혼합 비율(0~1) — 클수록 중심점 주변에 둥글게 뭉치고,
            /// 작을수록 노이즈가 지배적이라 해안선이 조각조각 들쭉날쭉해진다.</summary>
            public readonly float RadialWeight;
            /// <summary>true면 맨 바깥 테두리 칸에 큰 점수 보너스를 줘서 순위 컷에서 거의 항상 육지로
            /// 살아남게 한다(Lakes의 "가장자리 육지 다리" 규칙).</summary>
            public readonly bool ForceBorderLand;

            public MapShapeParams(int centerCount, float radialWeight, bool forceBorderLand)
            {
                CenterCount = centerCount;
                RadialWeight = radialWeight;
                ForceBorderLand = forceBorderLand;
            }
        }

        /// <summary>수도/Suburb/Pre-terrain 마을 위치(guaranteedLand)는 프리셋과 무관하게 항상 육지로
        /// 보호해야 하므로(7차 재정비), 이전에 Waterworld 전용이었던 "앵커 보호" 플래그는 더 이상 프리셋별
        /// 파라미터가 아니다 — GenerateMapShapeLandMask가 guaranteedLand 인자를 받아 항상 보호한다.</summary>
        private static MapShapeParams GetShapeParams(MapShapeMode mode, int biomeCount) => mode switch
        {
            // 중앙 대륙 + 외곽 바다(2/7.5절).
            MapShapeMode.Pangea => new MapShapeParams(centerCount: 1, radialWeight: 0.65f, forceBorderLand: false),
            // 순수 노이즈(중심 없음)로 흩어진 호수 모양 + 가장자리는 항상 육지("육지 다리").
            MapShapeMode.Lakes => new MapShapeParams(centerCount: 0, radialWeight: 0f, forceBorderLand: true),
            // 바이옴 수만큼 중심점을 흩어 각자 뚜렷한 대륙 하나씩 — 노이즈 비중은 낮아 윤곽이 비교적 뚜렷함.
            MapShapeMode.Continents => new MapShapeParams(centerCount: Mathf.Max(1, biomeCount), radialWeight: 0.6f, forceBorderLand: false),
            // Continents와 같은 중심점 수지만 노이즈 비중이 훨씬 높아 조각조각 흩어진 섬 모양이 됨.
            MapShapeMode.Archipelago => new MapShapeParams(centerCount: Mathf.Max(1, biomeCount), radialWeight: 0.25f, forceBorderLand: false),
            // 거의 전부 물(목표 90~100%).
            MapShapeMode.Waterworld => new MapShapeParams(centerCount: Mathf.Max(1, biomeCount), radialWeight: 0.5f, forceBorderLand: false),
            _ => new MapShapeParams(1, 0.65f, false)
        };

        /// <summary>랜드마스 마스크 기반 파이프라인(공통, Pangea 전용 코드를 일반화). (1) 맵 타입별
        /// 파라미터로 랜드마스 마스크를 목표 물 비율에 정확히 맞춰 만들되, 수도+Suburb+Pre-terrain
        /// 마을 위치(guaranteedLand)는 항상 육지로 보호한다(7차 재정비 — 예전엔 Waterworld의 수도만
        /// 보호했지만, 이제 모든 프리셋의 모든 "사전 확정된 마을/수도"가 보호 대상이다), (2) 그래도
        /// 마스크 컷에서 탈락했을 경우를 대비해 가장 가까운 육지 칸으로 옮기는 안전망을 유지하고,
        /// (3) 마스크가 물인 칸은 그 칸이 속한 바이옴의 물 타일로 직접 채우고(MinDistance 등 개별 제약을
        /// 건너뛰어 바다가 실제로 하나로 이어지게 함), (4) 마스크가 육지인 칸은 기존 가중치 알고리즘으로
        /// 채우되 물 타일 엔트리는 후보에서 제외한다(마스크가 이미 육지/바다를 결정했으므로 이중으로
        /// 물이 섞이지 않도록).</summary>
        private static (Vector2Int[] Anchors, Vector2Int[] Suburbs, Vector2Int[] PreTerrain) GenerateWithShape(GridWorld grid,
            IReadOnlyList<BiomeCsvRow> biomes, Random rng, MapShapeMode mode, float waterFraction,
            Vector2Int[] capitalAnchors, Vector2Int[] suburbs, Vector2Int[] preTerrain)
        {
            var guaranteedLand = Combine(ExpandToSquare(grid, capitalAnchors, CapitalLandRadius), suburbs, preTerrain);
            // Continents는 원문 7.5절(대륙 30~200칸, 서로 1칸 이상 떨어짐, 개수는 인원/습도/맵 크기로 결정)대로
            // 전용 대륙 성장 방식으로, 나머지는 공통 "방사형 감쇠 + 노이즈 순위 컷" 마스크로 만든다.
            var landMask = mode == MapShapeMode.Continents
                ? GenerateContinentsLandMask(grid, rng, waterFraction, biomes.Count)
                : GenerateMapShapeLandMask(grid, rng, waterFraction, GetShapeParams(mode, biomes.Count), guaranteedLand);

            Vector2Int[] anchors, snappedSuburbs, snappedPreTerrain;
            if (capitalAnchors.Length > 0)
            {
                // 쿼드런트로 미리 정한 수도/마을 — 마스크 컷에서 탈락했을 때의 안전망 스냅 후, 스냅으로 서로
                // 붙어버린 마을은 버린다(간격 규칙이 스냅보다 우선).
                anchors = SnapAllToLand(grid, capitalAnchors, landMask);
                snappedSuburbs = FilterBySpacing(SnapAllToLand(grid, suburbs, landMask), anchors, CityMinDistance);
                snappedPreTerrain = FilterBySpacing(SnapAllToLand(grid, preTerrain, landMask), Concat(anchors, snappedSuburbs), CityMinDistance);
            }
            else
            {
                // Pangea/Continents(원문 7.5절 "본토에 마을을 포화 배치 -> 그중 일부를 수도로 전환"). 결과(수도도 마을
                // 간격 규칙을 지키는 도시 중 하나)는 같게 유지하되 순서를 뒤집었다 — 수도를 먼저 육지 전체에서 고르고
                // (서로 최대한 멀리 + 해안 선호 + Continents는 서로 다른 대륙), 그 수도들을 포함한 채 본토 마을을 포화
                // 배치한다. 수도 후보를 이미 놓인 마을 칸으로만 제한하면 격자처럼 듬성한 후보 때문에 수도 간격이
                // 눈에 띄게 좁아졌다(검증에서 1칸씩 모자람).
                anchors = SelectCapitalsOnLand(grid, landMask, biomes.Count, preferDistinctLandmass: mode == MapShapeMode.Continents, rng);
                snappedSuburbs = Array.Empty<Vector2Int>();
                snappedPreTerrain = PlanVillagesOnLand(grid, landMask, ensureEveryLandmass: mode == MapShapeMode.Continents, anchors, rng);
            }
            var biomeIndexPerCell = ComputeBiomeIndexPerCell(grid, anchors);
            var cities = Combine(anchors, snappedSuburbs, snappedPreTerrain);

            ClearGeneratedTiles(grid);
            var placedPositionsByType = BuildInitialPlacedPositions(grid);

            ApplyLandmassMask(grid, biomes, biomeIndexPerCell, landMask, placedPositionsByType);

            var landOnlyBiomes = StripWaterTiles(biomes);
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, biomes.Count);
            PlaceMinCountQuota(grid, landOnlyBiomes, biomeIndexPerCell, regionSizePerBiome, placedPositionsByType, rng);
            FillRemaining(grid, landOnlyBiomes, biomeIndexPerCell, cities, placedPositionsByType, rng);

            ApplyForestAndMountains(grid, biomes, biomeIndexPerCell, cities, rng);
            return (anchors, snappedSuburbs, snappedPreTerrain);
        }

        private static Vector2Int[] Combine(Vector2Int[] a, Vector2Int[] b, Vector2Int[] c)
        {
            var result = new Vector2Int[a.Length + b.Length + c.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            c.CopyTo(result, a.Length + b.Length);
            return result;
        }

        private static Vector2Int[] Concat(Vector2Int[] a, Vector2Int[] b) => Combine(a, b, Array.Empty<Vector2Int>());

        /// <summary>positions를 순서대로 보며, existing과 이미 통과한 칸 전부로부터 minDistance(체비쇼프) 이상인
        /// 것만 남긴다(중복 칸도 여기서 걸러진다).</summary>
        private static Vector2Int[] FilterBySpacing(Vector2Int[] positions, Vector2Int[] existing, int minDistance)
        {
            var kept = new List<Vector2Int>(existing);
            var result = new List<Vector2Int>();
            foreach (var p in positions)
            {
                if (IsWithinDistance(kept, p, minDistance)) continue;
                kept.Add(p);
                result.Add(p);
            }
            return result.ToArray();
        }

        /// <summary>Pangea/Continents 본토 마을(원문 7.5절) — 육지 칸을 셔플된 순서로 보며, 가장자리 여백
        /// (PostTerrainVillageEdgeMargin)과 도시 간 간격(PostTerrainCityMinDistance, 이미 정해진 수도 포함)을 지키는
        /// 칸에 더 이상 자리가 없을 때까지 마을을 놓는다. ensureEveryLandmass면(Continents "모든 대륙에 최소 1개
        /// 마을") 포화 전에 아직 도시가 없는 대륙(4방향 연결 육지 덩어리)마다 하나씩 먼저 놓는다 — 작은 대륙은
        /// 가장자리 여백을 풀어서라도 놓는다. 새로 놓은 마을만 반환한다.</summary>
        private static Vector2Int[] PlanVillagesOnLand(GridWorld grid, bool[] landMask, bool ensureEveryLandmass, Vector2Int[] capitals, Random rng)
        {
            var landCells = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (landMask[grid.Index(new Vector2Int(x, y))]) landCells.Add(new Vector2Int(x, y));
            ProceduralGenerationUtil.Shuffle(landCells, rng);

            var cities = new List<Vector2Int>(capitals);
            var villages = new List<Vector2Int>();
            if (ensureEveryLandmass)
            {
                var (componentPerCell, componentSizes) = LabelLandComponents(grid, landMask);
                var covered = new HashSet<int>();
                foreach (var c in capitals) covered.Add(componentPerCell[grid.Index(c)]);
                for (int component = 1; component < componentSizes.Count; component++)
                {
                    if (covered.Contains(component)) continue;
                    for (int margin = PostTerrainVillageEdgeMargin; margin >= 0; margin--)
                    {
                        Vector2Int? pick = null;
                        foreach (var p in landCells)
                        {
                            if (componentPerCell[grid.Index(p)] != component) continue;
                            if (ProceduralGenerationUtil.DistanceToEdge(grid, p) < margin) continue;
                            if (IsWithinDistance(cities, p, PostTerrainCityMinDistance)) continue;
                            pick = p;
                            break;
                        }
                        if (!pick.HasValue) continue;
                        villages.Add(pick.Value);
                        cities.Add(pick.Value);
                        break;
                    }
                }
            }

            foreach (var p in landCells)
            {
                if (ProceduralGenerationUtil.DistanceToEdge(grid, p) < PostTerrainVillageEdgeMargin) continue;
                if (IsWithinDistance(cities, p, PostTerrainCityMinDistance)) continue;
                villages.Add(p);
                cities.Add(p);
            }
            return villages.ToArray();
        }

        /// <summary>Continents 전용 랜드마스 마스크(원문 7.5절). 대륙 수 = 인원 수를 기본으로 하되, 대륙 하나가
        /// ContinentMinSize~ContinentMaxSize(30~200칸)가 되도록 목표 육지 면적(= 맵 x (1-물 비율))으로 제한한다
        /// (예: 196칸 + 2명 + 물 절반 -> 대륙 2개 x 약 50칸). 대륙 씨앗은 서로 최대한 멀리(farthest-point) 뽑고,
        /// 라운드 로빈으로 한 칸씩 노이즈 점수가 가장 높은 경계 칸을 붙여 키운다. 다른 대륙 칸과 8방향으로 맞닿는
        /// 칸은 붙이지 않아 대륙끼리 항상 1칸 이상 물로 떨어진다(그 틈이 원문의 "1칸 폭 얕은 물 줄기(강)").</summary>
        private static bool[] GenerateContinentsLandMask(GridWorld grid, Random rng, float waterFraction, int playerCount)
        {
            int total = grid.Width * grid.Height;
            int landTarget = Mathf.Clamp(Mathf.RoundToInt(total * (1f - waterFraction)), 1, total);
            int minCount = Mathf.Max(1, Mathf.CeilToInt(landTarget / (float)ContinentMaxSize));
            int maxCount = Mathf.Max(minCount, landTarget / ContinentMinSize);
            int continentCount = Mathf.Clamp(Mathf.Max(1, playerCount), minCount, maxCount);

            // 씨앗: 가장자리 2칸 안쪽에서 첫 칸은 랜덤, 이후는 기존 씨앗들과의 최소 거리가 가장 큰 칸.
            var seedCandidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (ProceduralGenerationUtil.DistanceToEdge(grid, p) >= CapitalEdgeMargin) seedCandidates.Add(p);
                }
            if (seedCandidates.Count == 0)
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        seedCandidates.Add(new Vector2Int(x, y));
            ProceduralGenerationUtil.Shuffle(seedCandidates, rng);

            var seeds = new List<Vector2Int> { seedCandidates[0] };
            while (seeds.Count < continentCount && seeds.Count < seedCandidates.Count)
            {
                Vector2Int best = seedCandidates[0];
                float bestDist = -1f;
                foreach (var c in seedCandidates)
                {
                    float d = MinEuclideanDistance(seeds, c);
                    if (d > bestDist) { bestDist = d; best = c; }
                }
                seeds.Add(best);
            }

            int noiseSeedOffset = rng.Next(0, 1_000_000);
            var scores = new float[total];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    scores[grid.Index(new Vector2Int(x, y))] = NoiseSystem.Sample(x, y, frequency: 0.18f, octaves: 3, noiseSeedOffset) + (float)rng.NextDouble() * 0.2f;

            var owner = new int[total];
            for (int i = 0; i < total; i++) owner[i] = -1;
            var targets = new int[seeds.Count];
            var sizes = new int[seeds.Count];
            var frontiers = new List<HashSet<Vector2Int>>();
            for (int c = 0; c < seeds.Count; c++)
            {
                targets[c] = landTarget / seeds.Count + (c < landTarget % seeds.Count ? 1 : 0);
                frontiers.Add(new HashSet<Vector2Int>());
            }

            bool CanJoin(Vector2Int p, int c)
            {
                if (owner[grid.Index(p)] != -1) return false;
                foreach (var n in grid.GetNeighbors(p, allowDiagonal: true))
                {
                    int o = owner[grid.Index(n)];
                    if (o != -1 && o != c) return false;
                }
                return true;
            }

            void Claim(Vector2Int p, int c)
            {
                owner[grid.Index(p)] = c;
                sizes[c]++;
                frontiers[c].Remove(p);
                foreach (var n in grid.GetNeighbors(p, allowDiagonal: false))
                    if (owner[grid.Index(n)] == -1) frontiers[c].Add(n);
            }

            for (int c = 0; c < seeds.Count; c++)
                if (CanJoin(seeds[c], c)) Claim(seeds[c], c);

            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int c = 0; c < seeds.Count; c++)
                {
                    if (sizes[c] == 0 || sizes[c] >= targets[c]) continue;
                    Vector2Int? pick = null;
                    float pickScore = float.MinValue;
                    var blocked = new List<Vector2Int>();
                    foreach (var p in frontiers[c])
                    {
                        if (!CanJoin(p, c)) { blocked.Add(p); continue; }
                        float s = scores[grid.Index(p)];
                        // 동점일 때 결과가 HashSet 순회 순서에 좌우되지 않도록 좌표로 순서를 고정한다(시드 재현성).
                        if (s > pickScore || (Mathf.Approximately(s, pickScore) && pick.HasValue && (p.y < pick.Value.y || (p.y == pick.Value.y && p.x < pick.Value.x))))
                        {
                            pickScore = s;
                            pick = p;
                        }
                    }
                    foreach (var b in blocked) frontiers[c].Remove(b);
                    if (!pick.HasValue) continue;
                    Claim(pick.Value, c);
                    grew = true;
                }
            }

            var mask = new bool[total];
            for (int i = 0; i < total; i++) mask[i] = owner[i] != -1;
            return mask;
        }

        /// <summary>중심점(들)로부터의 방사형 감쇠 + 노이즈를 섞어 칸마다 "육지 점수"를 매기고, 점수 상위
        /// (1-waterFraction) 비율만큼을 육지로 확정한다 — 임계값을 눈대중으로 튜닝하는 대신 정확히 목표
        /// 물 비율을 맞추기 위해 순위 기반으로 자른다. CenterCount==0이면 방사형 항 없이 순수 노이즈만
        /// 쓴다(Lakes). ForceBorderLand는 순위 컷 전에 테두리 칸 점수에, guaranteedLand는 그 칸들 점수에
        /// 큰 보너스를 더해 사실상 육지로 보장한다.</summary>
        private static bool[] GenerateMapShapeLandMask(GridWorld grid, Random rng, float waterFraction, MapShapeParams shapeParams, Vector2Int[] guaranteedLand)
        {
            Vector2Int[] centers;
            if (shapeParams.CenterCount <= 0) centers = Array.Empty<Vector2Int>();
            else if (shapeParams.CenterCount == 1) centers = new[] { new Vector2Int(Mathf.RoundToInt((grid.Width - 1) * 0.5f), Mathf.RoundToInt((grid.Height - 1) * 0.5f)) };
            else centers = GenerateQuadrantAnchors(grid, shapeParams.CenterCount, rng);

            float maxDist = new Vector2(grid.Width, grid.Height).magnitude;
            int noiseSeedOffset = rng.Next(0, 1_000_000);
            const float BorderBonus = 10f; // 순위 컷에서 거의 항상 살아남을 만큼 큰 보너스(점수 범위 0~1보다 훨씬 큼)
            const float GuaranteeBonus = 20f; // 테두리 보너스보다도 커서 항상 가장 먼저 육지가 된다

            var guaranteedSet = new HashSet<int>();
            foreach (var p in guaranteedLand)
                if (grid.InBounds(p)) guaranteedSet.Add(grid.Index(p));

            var order = new List<int>(grid.Width * grid.Height);
            var scores = new float[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);
                    float noise = NoiseSystem.Sample(x, y, frequency: 0.12f, octaves: 3, noiseSeedOffset);

                    float score;
                    if (centers.Length == 0)
                    {
                        score = noise;
                    }
                    else
                    {
                        float nearestDist = float.MaxValue;
                        foreach (var c in centers)
                        {
                            float d = Vector2.Distance(new Vector2(x, y), c);
                            if (d < nearestDist) nearestDist = d;
                        }
                        float radial = maxDist > 0f ? 1f - Mathf.Clamp01(nearestDist / maxDist) : 1f;
                        score = radial * shapeParams.RadialWeight + noise * (1f - shapeParams.RadialWeight);
                    }

                    if (shapeParams.ForceBorderLand && ProceduralGenerationUtil.DistanceToEdge(grid, pos) == 0)
                        score += BorderBonus;

                    if (guaranteedSet.Contains(index))
                        score += GuaranteeBonus;

                    scores[index] = score;
                    order.Add(index);
                }
            }
            order.Sort((a, b) => scores[b].CompareTo(scores[a])); // 점수 내림차순 — 점수 높은 칸부터 육지

            // 보장 칸(수도 3x3/마을)은 목표 물 비율보다 우선한다 — Waterworld처럼 육지 목표가 보장 칸 수보다
            // 적어도 전부 육지가 되도록("Waterworld: 도시 자리의 땅은 강제로 생성", 2절).
            int landCount = Mathf.Max(Mathf.RoundToInt(order.Count * (1f - waterFraction)), guaranteedSet.Count);
            var mask = new bool[grid.Width * grid.Height];
            for (int i = 0; i < landCount; i++) mask[order[i]] = true;
            return mask;
        }

        /// <summary>각 위치가 이미 육지 칸이면 그대로, 바다 칸이면 가장 가까운 육지 칸으로 옮긴다 —
        /// GenerateMapShapeLandMask의 guaranteedLand 보너스가 극단적인 물 비율(예: Waterworld) 아래서도
        /// 거의 항상 육지를 보장하지만, 혹시 컷에서 탈락했을 경우의 안전망이다.</summary>
        private static Vector2Int[] SnapAllToLand(GridWorld grid, Vector2Int[] positions, bool[] landMask)
        {
            var result = new Vector2Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                result[i] = landMask[grid.Index(positions[i])] ? positions[i] : FindNearestLandCell(grid, positions[i], landMask);
            return result;
        }

        /// <summary>start를 중심으로 점점 넓어지는 정사각 테두리(체비쇼프 반지름)를 훑어 가장 가까운
        /// 육지 칸을 찾는다. 그리드가 작아(최대 30x30) 성능 문제는 없다.</summary>
        private static Vector2Int FindNearestLandCell(GridWorld grid, Vector2Int start, bool[] landMask)
        {
            int maxRadius = grid.Width + grid.Height;
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 테두리만
                        var p = start + new Vector2Int(dx, dy);
                        if (!grid.InBounds(p)) continue;
                        if (landMask[grid.Index(p)]) return p;
                    }
                }
            }
            return start; // 이론상 도달 안 함(육지가 하나도 없을 때만) — 안전한 폴백
        }

        private static int[] ComputeBiomeIndexPerCell(GridWorld grid, Vector2Int[] anchors)
        {
            var result = new int[grid.Width * grid.Height];
            if (anchors.Length <= 1) return result;

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    result[grid.Index(pos)] = ProceduralGenerationUtil.NearestAnchorIndex(anchors, pos);
                }
            return result;
        }

        /// <summary>랜드마스 마스크가 물인 칸을 그 칸이 속한 바이옴의 물 타일로 직접 채운다 — 개별 타일의
        /// MinDistance/가중치 계산을 거치지 않으므로, 이 칸들이 실제로 하나로 이어진 바다를 이룰 수 있다
        /// (기존 방식은 물 타일끼리 MinDistance 제약이 있어 바다가 절대 뭉칠 수 없었다 — 판게아가
        /// 의도한 것과 정반대였던 원인).</summary>
        private static void ApplyLandmassMask(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            bool[] landMask, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);
                    if (landMask[index]) continue; // 육지 칸은 이후 PlaceMinCountQuota/FillRemaining이 채움
                    if (grid.IsOccupied(pos)) continue;

                    var biome = biomes[biomeIndexPerCell[index]];
                    string waterTileId = FindFirstWaterTileId(biome);
                    if (waterTileId == null)
                    {
                        Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}'에 Water 타입 타일이 정의되어 있지 않아 판게아 바다 칸 {pos}을 채우지 못했습니다.");
                        continue;
                    }

                    grid.SetTerrain(pos, TerrainType.Water);
                    grid.SetTileType(pos, waterTileId);
                    AddPlacedPosition(placedPositionsByType, waterTileId, pos);
                }
            }
        }

        private static string FindFirstWaterTileId(BiomeCsvRow biome)
        {
            foreach (var tile in biome.Tiles)
                if (tile.TerrainType == TerrainType.Water) return tile.TileId;
            return null;
        }

        /// <summary>Freeform 경로 전용 — 마스크가 없어 사전에 육지를 보장할 수 없으므로, 채우기가 끝난
        /// 뒤 positions의 각 칸을 그 칸이 속한 바이옴의 첫 Land 타일로 강제 덮어쓴다(ApplyLandmassMask가
        /// 물 칸을 강제로 채우는 것과 대칭되는 육지판). 이미 유닛이 점유한 칸은 건드리지 않는다.</summary>
        private static void ForceLandAt(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Vector2Int[] positions, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            foreach (var pos in positions)
            {
                if (!grid.InBounds(pos) || grid.IsOccupied(pos)) continue;
                if (grid.GetTerrain(pos) == TerrainType.Land) continue; // 이미 육지면 손댈 필요 없음

                var biome = biomes[biomeIndexPerCell[grid.Index(pos)]];
                string landTileId = FindFirstLandTileId(biome);
                if (landTileId == null)
                {
                    Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}'에 Land 타입 타일이 정의되어 있지 않아 수도/마을 칸 {pos}을 육지로 강제하지 못했습니다.");
                    continue;
                }

                grid.SetTerrain(pos, TerrainType.Land);
                grid.SetTileType(pos, landTileId);
                AddPlacedPosition(placedPositionsByType, landTileId, pos);
            }
        }

        private static string FindFirstLandTileId(BiomeCsvRow biome)
        {
            foreach (var tile in biome.Tiles)
                if (tile.TerrainType == TerrainType.Land) return tile.TileId;
            return null;
        }

        /// <summary>docs/PolytopiaMapGeneration.md 4.2~4.4절 — 지형이 생기기 전에 먼저 정해지는 마을
        /// 두 종류(Suburb/Pre-terrain)를 계산한다. 7.1절 매트릭스대로 맵 타입마다 있을 수도/없을 수도
        /// 있다: Suburb는 Lakes/Archipelago 전용, Pre-terrain은 Lakes/Archipelago/Waterworld 전용,
        /// 나머지(Drylands/Pangea/Continents)는 둘 다 없음(빈 배열).</summary>
        private static (Vector2Int[] Suburbs, Vector2Int[] PreTerrain) PlanPreTerrainVillages(GridWorld grid, MapShapeMode shapeMode, Vector2Int[] capitalAnchors, Random rng)
        {
            bool hasSuburbs = shapeMode == MapShapeMode.Lakes || shapeMode == MapShapeMode.Archipelago;
            bool hasPreTerrain = shapeMode == MapShapeMode.Lakes || shapeMode == MapShapeMode.Archipelago || shapeMode == MapShapeMode.Waterworld;

            var reserved = new List<Vector2Int>(capitalAnchors);
            var suburbs = new List<Vector2Int>();
            if (hasSuburbs)
            {
                foreach (var capital in capitalAnchors)
                {
                    // 항상 2개를 시도한다(7.2절 "최대 2개, 보통 2개") — 0~1개는 자리가 없어 실패했을 때만 나온다
                    // (그래서 원문처럼 작은 맵일수록 1개가 더 흔하다).
                    for (int i = 0; i < SuburbsPerCapital; i++)
                    {
                        var pos = FindSuburbCell(grid, capital, reserved, rng);
                        if (!pos.HasValue) continue;
                        suburbs.Add(pos.Value);
                        reserved.Add(pos.Value);
                    }
                }
            }

            var preTerrain = new List<Vector2Int>();
            if (hasPreTerrain)
            {
                float density = shapeMode == MapShapeMode.Waterworld ? 0.1f : 0.3f; // 4.4절 밀도 계수
                int widthThird = Mathf.FloorToInt(grid.Width / 3f);
                int target = Mathf.Max(0, Mathf.RoundToInt((widthThird * widthThird - reserved.Count) * density));

                var candidates = new List<Vector2Int>();
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        candidates.Add(new Vector2Int(x, y));
                ProceduralGenerationUtil.Shuffle(candidates, rng);

                foreach (var pos in candidates)
                {
                    if (preTerrain.Count >= target) break;
                    if (ProceduralGenerationUtil.DistanceToEdge(grid, pos) < 1) continue; // 4.4절: 가장자리 최소 1칸

                    bool tooClose = false;
                    foreach (var r in reserved)
                        if (ProceduralGenerationUtil.ChebyshevDistance(r, pos) < CityMinDistance) { tooClose = true; break; } // 7.3절: 다른 마을/수도로부터 2칸
                    if (tooClose) continue;

                    preTerrain.Add(pos);
                    reserved.Add(pos);
                }
            }

            return (suburbs.ToArray(), preTerrain.ToArray());
        }

        /// <summary>capital로부터 SuburbRadius 이내에서 Suburb 자리를 찾는다 — 가장자리 1칸 여백 + 이미 정해진
        /// 수도/마을 전부와 CityMinDistance 이상(12절 보강 4: 예전엔 거리 검사가 없어 수도/다른 Suburb에 바로
        /// 붙을 수 있었다). 못 찾으면 null(Suburb는 "0개나 1개도 나올 수 있음"이라 실패해도 괜찮다).</summary>
        private static Vector2Int? FindSuburbCell(GridWorld grid, Vector2Int capital, List<Vector2Int> reserved, Random rng)
        {
            var candidates = new List<Vector2Int>();
            for (int dy = -SuburbRadius; dy <= SuburbRadius; dy++)
                for (int dx = -SuburbRadius; dx <= SuburbRadius; dx++)
                {
                    var p = capital + new Vector2Int(dx, dy);
                    if (!grid.InBounds(p) || ProceduralGenerationUtil.DistanceToEdge(grid, p) < 1) continue;
                    if (IsWithinDistance(reserved, p, CityMinDistance)) continue;
                    candidates.Add(p);
                }
            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>positions 중 pos와 체비쇼프 거리가 minDistance 미만인 것이 하나라도 있으면 true.</summary>
        private static bool IsWithinDistance(IEnumerable<Vector2Int> positions, Vector2Int pos, int minDistance)
        {
            foreach (var other in positions)
                if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < minDistance) return true;
            return false;
        }

        /// <summary>각 위치를 중심으로 반경 radius(체비쇼프) 정사각형 안의 칸들을 중복 없이 모은다(격자 밖 제외).</summary>
        private static Vector2Int[] ExpandToSquare(GridWorld grid, Vector2Int[] positions, int radius)
        {
            var result = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            foreach (var center in positions)
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        var p = center + new Vector2Int(dx, dy);
                        if (grid.InBounds(p) && seen.Add(p)) result.Add(p);
                    }
            return result.ToArray();
        }

        /// <summary>바이옴 목록을 복제하되 TerrainType이 Water인 타일 엔트리를 전부 뺀다 — 판게아 모드의
        /// 육지 채우기 단계는 랜드마스 마스크가 이미 "여긴 육지"라고 확정한 칸만 다루므로, 물 타일이
        /// 후보로 섞여 들어오면 안 된다.</summary>
        private static List<BiomeCsvRow> StripWaterTiles(IReadOnlyList<BiomeCsvRow> biomes)
        {
            var result = new List<BiomeCsvRow>(biomes.Count);
            foreach (var biome in biomes)
            {
                var clone = new BiomeCsvRow
                {
                    Id = biome.Id,
                    Name = biome.Name,
                    NoiseType = biome.NoiseType,
                    Frequency = biome.Frequency,
                    Octaves = biome.Octaves,
                    SeedOffset = biome.SeedOffset,
                    InnerRadius = biome.InnerRadius,
                    MountainRate = biome.MountainRate,
                    ForestRate = biome.ForestRate,
                    Structures = biome.Structures
                };
                foreach (var entry in biome.Tiles)
                    if (entry.TerrainType != TerrainType.Water) clone.Tiles.Add(entry);
                result.Add(clone);
            }
            return result;
        }

        /// <summary>Water 타입 엔트리만 배율을 적용한 바이옴 목록 복제본을 만든다. InnerWeight/OuterWeight는
        /// 곱하고(직접적인 확률 계수라 배율이 클수록 그대로 더 잘 나옴), CountPerTiles는 나눈다(개수 =
        /// 영역 크기/CountPerTiles라 값이 작아질수록 개수가 늘어나므로 — 습도가 높을수록 더 흔해지려면
        /// 나눠야 한다). 원본 biomes 리스트/엔트리는 값 타입(struct) 복사라 전혀 변경되지 않는다.</summary>
        private static List<BiomeCsvRow> ApplyWetness(IReadOnlyList<BiomeCsvRow> biomes, float wetnessMultiplier)
        {
            var result = new List<BiomeCsvRow>(biomes.Count);
            foreach (var biome in biomes)
            {
                var clone = new BiomeCsvRow
                {
                    Id = biome.Id,
                    Name = biome.Name,
                    NoiseType = biome.NoiseType,
                    Frequency = biome.Frequency,
                    Octaves = biome.Octaves,
                    SeedOffset = biome.SeedOffset,
                    InnerRadius = biome.InnerRadius,
                    MountainRate = biome.MountainRate,
                    ForestRate = biome.ForestRate,
                    Structures = biome.Structures
                };
                foreach (var entry in biome.Tiles)
                {
                    var e = entry;
                    if (e.TerrainType == TerrainType.Water)
                    {
                        e.InnerWeight *= wetnessMultiplier;
                        e.OuterWeight *= wetnessMultiplier;
                        if (e.CountPerTiles > 0f && wetnessMultiplier > 0f) e.CountPerTiles /= wetnessMultiplier;
                    }
                    clone.Tiles.Add(e);
                }
                result.Add(clone);
            }
            return result;
        }

        /// <summary>재생성(다시 "지형 생성" 버튼을 누르는 경우) 시 이전 결과가 "이미 채워진 칸"으로
        /// 오인되지 않도록 리셋한다. 유닛이 점유한 칸은 건드리지 않는다(그 위 지형을 바꾸면 이동 판정이
        /// 깨질 수 있으므로 — TerrainGenerationSystem은 항상 빈 칸만 다시 채운다).</summary>
        private static void ClearGeneratedTiles(GridWorld grid)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos)) continue;
                    grid.SetTileType(pos, string.Empty);
                }
        }

        /// <summary>이미 유닛이 점유해 지우지 않은 칸(ClearGeneratedTiles가 건너뛴 칸)의 TileTypeId를
        /// MinDistance 판정의 초기 상태로 삼는다 — 재생성 시 기존 배치와의 거리 제약도 지켜지도록.</summary>
        private static Dictionary<string, List<Vector2Int>> BuildInitialPlacedPositions(GridWorld grid)
        {
            var result = new Dictionary<string, List<Vector2Int>>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var tileType = grid.GetTileType(pos);
                    if (string.IsNullOrEmpty(tileType)) continue;
                    AddPlacedPosition(result, tileType, pos);
                }
            return result;
        }

        /// <summary>바이옴(수도) 수만큼 쿼드런트를 나눠 서로 다른 구역에서 앵커를 하나씩 뽑는다(6절 공정성 배치).
        /// 구역 수는 원문대로 1~4명=4, 5~9명=9, 10~16명=16. 8차 재정비(12절 보강 1/3) — 간격을 고르게 하려고
        /// (1) 빈 구역이 남을 때는 서로 가장 먼 구역부터 채우고(PickSpreadQuadrants), (2) 구역 안에서도 구역
        /// 중심 근처(구역 한 변의 1/5 반경)만 후보로 삼고 구역 경계 칸은 제외하며, (3) 맵 가장자리로부터
        /// CapitalEdgeMargin 이상, 이미 뽑힌 앵커와 CapitalMinDistance 이상 떨어진 칸만 쓴다. 조건을 만족하는
        /// 칸이 없으면(아주 작은 맵) 조건을 하나씩 풀어 폴백한다. 랜드마스 마스크의 대륙 중심점도 이 함수로 뽑는다.</summary>
        private static Vector2Int[] GenerateQuadrantAnchors(GridWorld grid, int biomeCount, Random rng)
        {
            int quadrantsPerSide = biomeCount <= 4 ? 2 : biomeCount <= 9 ? 3 : biomeCount <= 16 ? 4 : Mathf.CeilToInt(Mathf.Sqrt(biomeCount));
            var quadrantIndices = PickSpreadQuadrants(quadrantsPerSide, biomeCount, rng);

            var anchors = new Vector2Int[biomeCount];
            var chosen = new List<Vector2Int>();
            for (int b = 0; b < biomeCount; b++)
            {
                int q = quadrantIndices[b];
                var (xMin, xMax) = QuadrantRange(grid.Width, quadrantsPerSide, q % quadrantsPerSide);
                var (yMin, yMax) = QuadrantRange(grid.Height, quadrantsPerSide, q / quadrantsPerSide);
                anchors[b] = PickCellInQuadrant(grid, xMin, xMax, yMin, yMax, chosen, rng);
                chosen.Add(anchors[b]);
            }
            return anchors;
        }

        /// <summary>count개의 구역 인덱스를 고른다. 구역이 남으면(count &lt; 전체) 첫 구역은 랜덤, 이후로는 이미
        /// 고른 구역들과의 최소 거리(구역 좌표 기준)가 가장 큰 구역을 고른다(동점은 랜덤) — 2명이면 대각선,
        /// 3명이면 세 모서리가 된다. 구역이 모자라면(count &gt; 전체) 셔플한 전체 목록을 반복한다.</summary>
        private static List<int> PickSpreadQuadrants(int quadrantsPerSide, int count, Random rng)
        {
            int total = quadrantsPerSide * quadrantsPerSide;
            var all = new List<int>();
            for (int i = 0; i < total; i++) all.Add(i);
            ProceduralGenerationUtil.Shuffle(all, rng);

            var result = new List<int>();
            if (count >= total)
            {
                for (int i = 0; i < count; i++) result.Add(all[i % total]);
                return result;
            }

            result.Add(all[0]);
            while (result.Count < count)
            {
                int best = -1;
                float bestDist = -1f;
                foreach (var q in all) // 셔플된 순서라 동점이면 자연스럽게 랜덤
                {
                    if (result.Contains(q)) continue;
                    float minDist = float.MaxValue;
                    foreach (var r in result)
                    {
                        float d = Vector2.Distance(new Vector2(q % quadrantsPerSide, q / quadrantsPerSide), new Vector2(r % quadrantsPerSide, r / quadrantsPerSide));
                        if (d < minDist) minDist = d;
                    }
                    if (minDist > bestDist + 0.001f) { bestDist = minDist; best = q; }
                }
                result.Add(best);
            }
            ProceduralGenerationUtil.Shuffle(result, rng); // 바이옴 -> 구역 배정 순서는 랜덤
            return result;
        }

        /// <summary>[xMin,xMax)x[yMin,yMax) 구역에서 앵커 칸 하나를 고른다 — GenerateQuadrantAnchors 주석의
        /// 조건(중심 근처/구역 경계 제외/가장자리 여백/앵커 간 최소 거리)을 엄격한 것부터 차례로 풀며 시도한다.</summary>
        private static Vector2Int PickCellInQuadrant(GridWorld grid, int xMin, int xMax, int yMin, int yMax, List<Vector2Int> chosen, Random rng)
        {
            float cx = (xMin + xMax - 1) * 0.5f;
            float cy = (yMin + yMax - 1) * 0.5f;
            float radius = Mathf.Max(1, Mathf.Min(xMax - xMin, yMax - yMin) / 5) + 0.5f;

            for (int tier = 0; tier < 4; tier++)
            {
                var candidates = new List<Vector2Int>();
                for (int y = yMin; y < yMax; y++)
                    for (int x = xMin; x < xMax; x++)
                    {
                        var p = new Vector2Int(x, y);
                        if (tier < 1 && (Mathf.Abs(x - cx) > radius || Mathf.Abs(y - cy) > radius)) continue;
                        if (tier < 2 && IsOnInnerQuadrantBorder(grid, p, xMin, xMax, yMin, yMax)) continue;
                        if (tier < 3 && ProceduralGenerationUtil.DistanceToEdge(grid, p) < CapitalEdgeMargin) continue;
                        if (IsWithinDistance(chosen, p, tier < 3 ? CapitalMinDistance : 1)) continue;
                        candidates.Add(p);
                    }
                if (candidates.Count > 0) return candidates[rng.Next(candidates.Count)];
            }
            return new Vector2Int(rng.Next(xMin, xMax), rng.Next(yMin, yMax));
        }

        /// <summary>p가 구역의 안쪽 경계(맵 가장자리가 아닌, 다른 구역과 맞닿은 줄) 위에 있는지 — 이웃 구역의
        /// 앵커와 경계를 사이에 두고 딱 붙는 것을 막는다.</summary>
        private static bool IsOnInnerQuadrantBorder(GridWorld grid, Vector2Int p, int xMin, int xMax, int yMin, int yMax) =>
            (p.x == xMin && xMin > 0) || (p.x == xMax - 1 && xMax < grid.Width) ||
            (p.y == yMin && yMin > 0) || (p.y == yMax - 1 && yMax < grid.Height);

        /// <summary>Pangea/Continents 전용(7.5절) — 이미 만들어진 땅(landMask) 위에서 수도 count개를 고른다.
        /// 후보: 가장자리로부터 CapitalEdgeMargin 이상 + 크기 MinCapitalLandmassSize 이상인 육지 덩어리에 속한 칸
        /// (12절 보강 2/3 — 1칸 섬 수도 방지). 원문 기준 두 가지를 반영한다: (a) 수도끼리 최대한 멀리 — 첫 수도는
        /// 랜덤, 이후는 기존 수도들과의 최소 거리가 가장 큰 칸(farthest-point)을 고르고, 이를 여러 번 시도해 최소 쌍
        /// 거리가 가장 큰 조합을 채택(12절 보강 1), (b) 해안(물 인접) 선호 — 점수 보너스. Continents는 아직 수도가
        /// 없는 대륙에 큰 보너스를 줘 가능하면 서로 다른 대륙에 놓는다. 후보가 모자라면 조건을 풀고, 그래도
        /// 모자라면 쿼드런트 방식으로 뽑아 그 3x3을 마스크에서 육지로 바꾼다. 본토 마을은 이 수도들을 포함한 채
        /// 나중에 포화 배치된다(PlanVillagesOnLand).</summary>
        private static Vector2Int[] SelectCapitalsOnLand(GridWorld grid, bool[] landMask, int count, bool preferDistinctLandmass, Random rng)
        {
            var (componentPerCell, componentSizes) = LabelLandComponents(grid, landMask);

            List<Vector2Int> candidates = null;
            for (int tier = 0; tier < 3; tier++)
            {
                candidates = new List<Vector2Int>();
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var p = new Vector2Int(x, y);
                        int index = grid.Index(p);
                        if (!landMask[index]) continue;
                        if (tier < 2 && ProceduralGenerationUtil.DistanceToEdge(grid, p) < CapitalEdgeMargin) continue;
                        if (tier < 1 && componentSizes[componentPerCell[index]] < MinCapitalLandmassSize) continue;
                        candidates.Add(p);
                    }
                if (candidates.Count >= count) break;
            }

            if (candidates.Count < count)
            {
                var fallback = GenerateQuadrantAnchors(grid, count, rng);
                foreach (var p in ExpandToSquare(grid, fallback, CapitalLandRadius)) landMask[grid.Index(p)] = true;
                return fallback;
            }

            List<Vector2Int> best = null;
            float bestEval = float.MinValue;
            for (int trial = 0; trial < CapitalSelectionTrials; trial++)
            {
                var chosen = new List<Vector2Int> { candidates[rng.Next(candidates.Count)] };
                var usedComponents = new HashSet<int> { componentPerCell[grid.Index(chosen[0])] };
                while (chosen.Count < count)
                {
                    Vector2Int pick = default;
                    float pickScore = float.MinValue;
                    foreach (var c in candidates)
                    {
                        if (chosen.Contains(c)) continue;
                        float score = MinEuclideanDistance(chosen, c)
                                      + (IsCoastal(grid, c, landMask) ? 0.75f : 0f)
                                      + (preferDistinctLandmass && !usedComponents.Contains(componentPerCell[grid.Index(c)]) ? 100f : 0f)
                                      + (float)rng.NextDouble() * 0.25f;
                        if (score > pickScore) { pickScore = score; pick = c; }
                    }
                    chosen.Add(pick);
                    usedComponents.Add(componentPerCell[grid.Index(pick)]);
                }

                float minPair = chosen.Count < 2 ? 0f : float.MaxValue;
                int coastal = 0;
                for (int i = 0; i < chosen.Count; i++)
                {
                    if (IsCoastal(grid, chosen[i], landMask)) coastal++;
                    for (int j = i + 1; j < chosen.Count; j++)
                        minPair = Mathf.Min(minPair, Vector2.Distance(chosen[i], chosen[j]));
                }
                float eval = minPair + coastal * 0.3f;
                if (eval > bestEval) { bestEval = eval; best = chosen; }
            }
            return best.ToArray();
        }

        private static float MinEuclideanDistance(List<Vector2Int> positions, Vector2Int pos)
        {
            float min = float.MaxValue;
            foreach (var p in positions) min = Mathf.Min(min, Vector2.Distance(p, pos));
            return min;
        }

        /// <summary>상하좌우 이웃 중 물(마스크 false) 칸이 하나라도 있으면 해안.</summary>
        private static bool IsCoastal(GridWorld grid, Vector2Int pos, bool[] landMask)
        {
            foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                if (!landMask[grid.Index(n)]) return true;
            return false;
        }

        /// <summary>육지 칸을 4방향 연결 덩어리로 라벨링한다 — 칸별 덩어리 번호와 덩어리별 크기. 0번은 물 전용
        /// 더미(크기 0)라 육지 덩어리 번호는 1부터 시작한다.</summary>
        private static (int[] ComponentPerCell, List<int> ComponentSizes) LabelLandComponents(GridWorld grid, bool[] landMask)
        {
            var componentPerCell = new int[grid.Width * grid.Height];
            var sizes = new List<int> { 0 };
            var stack = new Stack<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    int startIndex = grid.Index(start);
                    if (!landMask[startIndex] || componentPerCell[startIndex] != 0) continue;

                    int id = sizes.Count;
                    int size = 0;
                    componentPerCell[startIndex] = id;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        size++;
                        foreach (var n in grid.GetNeighbors(p, allowDiagonal: false))
                        {
                            int ni = grid.Index(n);
                            if (!landMask[ni] || componentPerCell[ni] != 0) continue;
                            componentPerCell[ni] = id;
                            stack.Push(n);
                        }
                    }
                    sizes.Add(size);
                }
            return (componentPerCell, sizes);
        }

        /// <summary>산/숲 배수 -> 실제 육지 대비 산/숲 비율(4절 적용 순서): 산 = 14% x MountainRate를 먼저 정하고,
        /// 숲은 기준 38%를 "남은 비율(100%-산%)/86%"로 비례 보정한 뒤 ForestRate를 곱한다. 평지는 나머지.</summary>
        public static (float Mountain, float Forest) ComputeFeatureFractions(float mountainRate, float forestRate)
        {
            float mountain = Mathf.Clamp01(BaseMountainFraction * Mathf.Max(0f, mountainRate));
            float forest = BaseForestFraction * (1f - mountain) / (1f - BaseMountainFraction) * Mathf.Max(0f, forestRate);
            return (mountain, Mathf.Clamp(forest, 0f, 1f - mountain));
        }

        /// <summary>숲/산 레이어(12.1절) — 바이옴 영역마다 육지 칸(수도/마을 칸 제외) 중 ComputeFeatureFractions
        /// 비율만큼을 정확히(쿼터) 산, 그다음 숲으로 바꾼다(Balance Pass 2: "항상 비율대로"). 어느 칸이 될지는
        /// 산/숲 각자의 노이즈 순위로 정해 산맥/숲 덩어리로 뭉치게 한다. MountainRate/ForestRate가 둘 다 0인
        /// 바이옴(예전 CSV)은 건드리지 않는다.</summary>
        private static void ApplyForestAndMountains(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell, Vector2Int[] cityCells, Random rng)
        {
            var citySet = new HashSet<Vector2Int>(cityCells);
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
            {
                var biome = biomes[biomeIdx];
                if (biome.MountainRate <= 0f && biome.ForestRate <= 0f) continue;

                var cells = new List<Vector2Int>();
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var p = new Vector2Int(x, y);
                        if (biomeIndexPerCell[grid.Index(p)] != biomeIdx) continue;
                        if (grid.GetTerrain(p) != TerrainType.Land || grid.IsOccupied(p) || citySet.Contains(p)) continue;
                        if (string.IsNullOrEmpty(grid.GetTileType(p))) continue;
                        cells.Add(p);
                    }

                var (mountainFraction, forestFraction) = ComputeFeatureFractions(biome.MountainRate, biome.ForestRate);
                int mountainCount = Mathf.RoundToInt(cells.Count * mountainFraction);
                int forestCount = Mathf.RoundToInt(cells.Count * forestFraction);
                float frequency = biome.Frequency > 0f ? biome.Frequency : 0.15f;

                var remaining = AssignTopByNoise(grid, cells, mountainCount, MountainTileId, frequency * 2f, rng.Next(0, 1_000_000), rng);
                AssignTopByNoise(grid, remaining, forestCount, ForestTileId, frequency * 1.5f, rng.Next(0, 1_000_000), rng);
            }
        }

        /// <summary>cells를 노이즈 점수(+작은 랜덤) 내림차순으로 정렬해 상위 count칸의 TileTypeId를 tileId로 바꾸고,
        /// 나머지 칸 목록을 반환한다.</summary>
        private static List<Vector2Int> AssignTopByNoise(GridWorld grid, List<Vector2Int> cells, int count, string tileId, float frequency, int seedOffset, Random rng)
        {
            var scored = new List<(Vector2Int Pos, float Score)>(cells.Count);
            foreach (var p in cells)
                scored.Add((p, NoiseSystem.Sample(p.x, p.y, frequency, 2, seedOffset) + (float)rng.NextDouble() * 0.15f));
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            var remaining = new List<Vector2Int>(cells.Count);
            for (int i = 0; i < scored.Count; i++)
            {
                if (i < count) grid.SetTileType(scored[i].Pos, tileId);
                else remaining.Add(scored[i].Pos);
            }
            return remaining;
        }

        /// <summary>totalSize를 quadrantsPerSide등분한 구간 중 quadrantIndex번째의 [min, max) 범위.
        /// 나머지가 남는 경우 뒤쪽 구간이 한 칸씩 더 커지는 정도만 허용(균등 분할 근사).</summary>
        private static (int Min, int Max) QuadrantRange(int totalSize, int quadrantsPerSide, int quadrantIndex)
        {
            int min = quadrantIndex * totalSize / quadrantsPerSide;
            int max = (quadrantIndex + 1) * totalSize / quadrantsPerSide;
            max = Mathf.Min(Mathf.Max(max, min + 1), totalSize);
            min = Mathf.Min(min, totalSize - 1);
            return (min, max);
        }

        /// <summary>biomeIndexPerCell을 세서 바이옴별로 실제로 배정된 칸 수를 구한다 — CountPerTiles가
        /// "이 바이옴 영역 안에서 N칸당 하나" 밀도를 뜻하므로(맵 전체 크기가 아니라), 바이옴마다 자기
        /// 영역 크기를 알아야 한다.</summary>
        private static int[] CountRegionSizes(int[] biomeIndexPerCell, int biomeCount)
        {
            var sizes = new int[biomeCount];
            foreach (var idx in biomeIndexPerCell) sizes[idx]++;
            return sizes;
        }

        /// <summary>MinCount(또는 CountPerTiles로 그 바이옴 영역 크기에 비례해 계산한 개수 중 더 큰 값)가
        /// 지정된 엔트리부터, 그 바이옴 영역의 빈 칸 중 제약(EdgeMargin/MinDistance/ExcludeAdjacent)을
        /// 만족하는 칸을 셔플된 순서로 찾아 우선 배치한다. 다 채우지 못하면 경고만 남기고 넘어간다(막힌
        /// 칸 폴백은 FillRemaining이 어차피 나머지 빈 칸을 전부 채워주므로 생성 자체가 실패하지는 않는다).</summary>
        private static void PlaceMinCountQuota(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            int[] regionSizePerBiome, Dictionary<string, List<Vector2Int>> placedPositionsByType, Random rng)
        {
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
            {
                var biome = biomes[biomeIdx];
                for (int entryIdx = 0; entryIdx < biome.Tiles.Count; entryIdx++)
                {
                    var entry = biome.Tiles[entryIdx];
                    int targetCount = EffectiveMinCount(entry.MinCount, entry.CountPerTiles, regionSizePerBiome[biomeIdx]);
                    if (targetCount <= 0) continue;

                    var candidates = CollectEmptyCellsInBiome(grid, biomeIndexPerCell, biomeIdx);
                    ProceduralGenerationUtil.Shuffle(candidates, rng);

                    int placed = 0;
                    foreach (var pos in candidates)
                    {
                        if (placed >= targetCount) break;
                        if (ViolatesConstraints(grid, pos, entry, placedPositionsByType)) continue;
                        PlaceTile(grid, pos, entry, placedPositionsByType);
                        placed++;
                    }

                    if (placed < targetCount)
                        Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}' tile '{entry.TileId}': 목표 개수 {targetCount}개 중 {placed}개만 배치됨(공간 부족 또는 제약 충돌).");
                }
            }
        }

        /// <summary>MinCount와 CountPerTiles(이 바이옴/구조물 영역 크기에 비례, Polytopia의 유적/외딴섬
        /// 마을 개수표와 같은 개념) 중 더 큰 값을 실제 목표 개수로 쓴다. StructureGenerationSystem도
        /// 재사용한다.</summary>
        public static int EffectiveMinCount(int minCount, float countPerTiles, int regionSize)
        {
            int fromDensity = countPerTiles > 0f ? Mathf.RoundToInt(regionSize / countPerTiles) : 0;
            return Mathf.Max(minCount, fromDensity);
        }

        /// <summary>남은 빈 칸을 셔플된 순서로 순회하며, 제약(EdgeMargin/MinDistance/ExcludeAdjacent)을
        /// 위반하는 후보를 제거하고 남은 후보를 Inner/Outer 가중치 + 노이즈로 랜덤 선택한다. Inner/Outer는
        /// 가장 가까운 "이미 위치가 정해진 도시"(수도 + Suburb + 사전 확정 마을) 기준이다(원문 3절 "도시/마을에
        /// 인접한 칸" — 예전엔 수도 하나만 기준이었다).
        /// 후보가 하나도 안 남으면 그 바이옴의 첫 엔트리로 폴백한다(완전히 막힌 칸도 항상 무언가로
        /// 채워지도록).</summary>
        private static void FillRemaining(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Vector2Int[] cities, Dictionary<string, List<Vector2Int>> placedPositionsByType, Random rng)
        {
            var emptyCells = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetTileType(pos))) continue;
                    emptyCells.Add(pos);
                }
            ProceduralGenerationUtil.Shuffle(emptyCells, rng);

            foreach (var pos in emptyCells)
            {
                int biomeIdx = biomeIndexPerCell[grid.Index(pos)];
                var biome = biomes[biomeIdx];
                if (biome.Tiles.Count == 0) continue;
                int distToCity = int.MaxValue;
                foreach (var c in cities) distToCity = Mathf.Min(distToCity, ProceduralGenerationUtil.ChebyshevDistance(c, pos));

                var candidates = new List<(BiomeTileEntry Entry, float Weight)>();
                for (int i = 0; i < biome.Tiles.Count; i++)
                {
                    var entry = biome.Tiles[i];
                    if (ViolatesConstraints(grid, pos, entry, placedPositionsByType)) continue;
                    candidates.Add((entry, ComputeWeight(biome, i, entry, distToCity, pos.x, pos.y)));
                }

                var chosen = candidates.Count == 0 ? biome.Tiles[0] : ProceduralGenerationUtil.WeightedPick(candidates, rng);
                PlaceTile(grid, pos, chosen, placedPositionsByType);
            }
        }

        private static List<Vector2Int> CollectEmptyCellsInBiome(GridWorld grid, int[] biomeIndexPerCell, int biomeIdx)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (biomeIndexPerCell[grid.Index(pos)] != biomeIdx) continue;
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetTileType(pos))) continue;
                    candidates.Add(pos);
                }
            return candidates;
        }

        private static void PlaceTile(GridWorld grid, Vector2Int pos, BiomeTileEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            grid.SetTerrain(pos, entry.TerrainType);
            grid.SetTileType(pos, entry.TileId);
            AddPlacedPosition(placedPositionsByType, entry.TileId, pos);
        }

        private static void AddPlacedPosition(Dictionary<string, List<Vector2Int>> placedPositionsByType, string tileId, Vector2Int pos)
        {
            if (!placedPositionsByType.TryGetValue(tileId, out var list))
            {
                list = new List<Vector2Int>();
                placedPositionsByType[tileId] = list;
            }
            list.Add(pos);
        }

        /// <summary>EdgeMargin(가장자리 여백) / MinDistance(같은 타입끼리 최소 거리, 둘 다 체비쇼프
        /// 거리) / ExcludeAdjacent(바로 인접한 다른 타입 배제, 1차 구현부터 유지) 세 제약을 전부 검사한다.</summary>
        private static bool ViolatesConstraints(GridWorld grid, Vector2Int pos, BiomeTileEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            if (entry.EdgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, pos) < entry.EdgeMargin) return true;

            if (entry.MinDistance > 0 && placedPositionsByType.TryGetValue(entry.TileId, out var placed))
            {
                foreach (var other in placed)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < entry.MinDistance) return true;
            }

            if (entry.ExcludeAdjacent != null && entry.ExcludeAdjacent.Length > 0)
            {
                foreach (var neighborPos in grid.GetNeighbors(pos, allowDiagonal: false))
                {
                    var neighborType = grid.GetTileType(neighborPos);
                    if (string.IsNullOrEmpty(neighborType)) continue;

                    foreach (var excluded in entry.ExcludeAdjacent)
                        if (string.Equals(neighborType, excluded, StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }

            return false;
        }

        /// <summary>칸에서 가장 가까운 도시까지의 체비쇼프 거리(distToCity)가 InnerRadius 이내면
        /// InnerWeight, 아니면 OuterWeight를 기준값으로 쓰고, 엔트리마다 독립된 노이즈(SeedOffset을
        /// 엔트리별로 다르게)로 한 번 더 보정한다 — 그래야 숲/모래 같은 타입이 한 칸씩 흩뿌려지지 않고
        /// 자연스럽게 뭉친 패치로 나온다.</summary>
        private static float ComputeWeight(BiomeCsvRow biome, int entryIndex, BiomeTileEntry entry, int distToCity, int x, int y)
        {
            float baseWeight = distToCity <= biome.InnerRadius ? entry.InnerWeight : entry.OuterWeight;

            float noise = NoiseSystem.Sample(x, y, biome.Frequency, biome.Octaves, biome.SeedOffset + entryIndex * 997);
            return Mathf.Max(0f, baseWeight) * (NoiseWeightFloor + noise);
        }
    }
}
