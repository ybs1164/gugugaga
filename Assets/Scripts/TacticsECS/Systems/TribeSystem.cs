using System.Collections.Generic;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 종족(Tribes.csv)을 팀에 적용한다: 시작 별, 연구 가능한 기술(기술 그룹), 처음부터 해금된 기술, 시작 유닛.
    /// 지형 쪽(팀마다 종족 영역 바이옴 / 수도 주변 시작 조건)은 RegionBiomes/StartCondition으로 꺼내 지형 생성에 넘긴다.
    /// 자체 상태는 없다 — 결과는 EconomyWorld(Resources/Tech/StartUnitIds)에만 남는다.
    /// 규칙: docs/spec/game/tribe.md
    /// </summary>
    public static class TribeSystem
    {
        public static bool IsValid(int tribeIndex) => tribeIndex >= 0 && tribeIndex < GameTables.Tribes.Length;

        /// <summary>econ.Tech[team]/Resources[team]이 이미 만들어진 뒤 부른다. econ.TechNodes는 배열형 기술 표(TechGroupSystem.BuildTechNodes)여야
        /// Index가 맞는다. unitRows는 이번 전투의 유닛 표 — StartUnit(Units Index)의 Id로 찾는다.</summary>
        public static void Apply(EconomyWorld econ, Team team, TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var res = econ.Resources[team];
            res.Stars = tribe.StartStars;
            econ.Resources[team] = res;

            var tech = econ.Tech[team];
            if (tribe.TechGroupIndex >= 0 && tribe.TechGroupIndex < GameTables.TechGroups.Length)
                tech.Allowed = TechGroupSystem.GroupTechIds(GameTables.TechGroups[tribe.TechGroupIndex], GameTables.Techs);
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) tech.Unlocked.Add(GameTables.Techs[t].Id);
            econ.Tech[team] = tech;

            econ.StartUnitIds[team] = StartUnitIds(tribe, unitRows);
            econ.StartCapitalLevels[team] = System.Math.Max(1, tribe.StartCapitalLevel);
        }

        public static string[] StartUnitIds(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var ids = new List<string>();
            foreach (var row in StartUnitRows(tribe, unitRows)) ids.Add(row.Id);
            return ids.ToArray();
        }

        /// <summary>StartUnit{n}(Units Index)이 가리키는 유닛을 unitRows에서 Id로 찾는다 — 불러온 유닛 표에 그 Id가 없으면 빠진다.</summary>
        public static List<UnitCsvRow> StartUnitRows(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var rows = new List<UnitCsvRow>();
            foreach (var u in tribe.StartUnits ?? new int[0])
            {
                if (unitRows == null || u < 0 || u >= GameTables.Units.Length) continue;
                string id = GameTables.Units[u].Id;
                foreach (var row in unitRows)
                    if (row.Id == id) { rows.Add(row); break; }
            }
            return rows;
        }

        /// <summary>팀마다 영역 하나(CitySystem.Teams 순서) — 그 팀 종족의 영역 바이옴, 종족이 없으면 첫 바이옴 그대로.</summary>
        public static List<BiomeCsvRow> RegionBiomes(IReadOnlyList<TribeRow?> tribesPerTeam, IReadOnlyList<BiomeCsvRow> biomes)
        {
            var regions = new List<BiomeCsvRow>();
            if (biomes == null || biomes.Count == 0) return regions;
            foreach (var tribe in tribesPerTeam)
                regions.Add(tribe is TribeRow t ? RegionBiome(t, biomes) : biomes[0]);
            return regions;
        }

        /// <summary>종족의 기본 바이옴(Id가 없으면 첫 바이옴).</summary>
        public static BiomeCsvRow BaseBiome(TribeRow tribe, IReadOnlyList<BiomeCsvRow> biomes)
        {
            foreach (var b in biomes)
                if (b.Id == tribe.Biome) return b;
            return biomes[0];
        }

        /// <summary>종족 영역 바이옴 — 기본 바이옴을 복제해 평지 타일을 GroundTile로 바꾸고 배수를 곱한다. 원본 바이옴은 그대로 둔다.</summary>
        public static BiomeCsvRow RegionBiome(TribeRow tribe, IReadOnlyList<BiomeCsvRow> biomes)
        {
            var src = BaseBiome(tribe, biomes);
            string field = FieldTileId(src);
            string ground = string.IsNullOrEmpty(tribe.GroundTile) || field == null ? field : tribe.GroundTile;
            string Swap(string id) => id == field ? ground : id;
            string[] SwapAll(string[] ids)
            {
                if (ids == null) return null;
                var r = new string[ids.Length];
                for (int i = 0; i < ids.Length; i++) r[i] = Swap(ids[i]);
                return r;
            }

            var biome = new BiomeCsvRow
            {
                Id = src.Id, Name = src.Name, NoiseType = src.NoiseType, Frequency = src.Frequency, Octaves = src.Octaves,
                SeedOffset = src.SeedOffset, InnerRadius = src.InnerRadius,
                MountainRate = src.MountainRate * tribe.MountainRate,
                ForestRate = src.ForestRate * tribe.ForestRate,
            };
            foreach (var e in src.Tiles)
            {
                var t = e;
                t.TileId = Swap(e.TileId);
                t.ExcludeAdjacent = SwapAll(e.ExcludeAdjacent);
                biome.Tiles.Add(t);
            }
            foreach (var e in src.Structures)
            {
                var s = e;
                s.AllowedTileTypes = SwapAll(e.AllowedTileTypes);
                float rate = ResourceRate(tribe, e.StructureId);
                s.InnerRate = e.InnerRate * rate;
                s.OuterRate = e.OuterRate * rate;
                biome.Structures.Add(s);
            }
            return biome;
        }

        /// <summary>바이옴의 평지 = 첫 Land 타일 엔트리(없으면 null).</summary>
        public static string FieldTileId(BiomeCsvRow biome)
        {
            foreach (var t in biome.Tiles)
                if (t.TerrainType == TerrainType.Land) return t.TileId;
            return null;
        }

        public const string FruitId = "Resource_Fruit";
        public const string CropId = "Resource_Crop";
        public const string AnimalId = "Resource_Animal";
        public const string MetalId = "Resource_Metal";
        public const string FishId = "Resource_Fish";

        /// <summary>자원 구조물 Id에 맞는 종족 배수(자원이 아니면 1).</summary>
        public static float ResourceRate(TribeRow tribe, string structureId) => structureId switch
        {
            FruitId => tribe.FruitRate,
            CropId => tribe.CropRate,
            AnimalId => tribe.AnimalRate,
            MetalId => tribe.MetalRate,
            FishId => tribe.FishRate,
            _ => 1f,
        };

        /// <summary>종족의 시작 조건(없으면 null).</summary>
        public static StartConditionRow? StartCondition(TribeRow tribe) =>
            tribe.StartConditionIndex >= 0 && tribe.StartConditionIndex < GameTables.StartConditions.Length
                ? GameTables.StartConditions[tribe.StartConditionIndex] : (StartConditionRow?)null;

        /// <summary>샌드박스 정보 칸용 요약 한 줄: 바이옴/시작 기술/별/유닛/시작 조건.</summary>
        public static string Summary(TribeRow tribe, IReadOnlyList<BiomeCsvRow> biomes, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var sb = new StringBuilder();
            sb.Append(tribe.Name).Append(": ");
            sb.Append(biomes != null && biomes.Count > 0 ? BaseBiome(tribe, biomes).Name : "?").Append(" / ");
            var techs = new List<string>();
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) techs.Add(GameTables.Techs[t].Name);
            sb.Append(techs.Count > 0 ? string.Join(",", techs) : LocalizationSystem.T("UI.Tribe.NoTech"))
              .Append(" / ").Append(LocalizationSystem.F("UI.Tribe.Stars", tribe.StartStars));
            var units = new List<string>();
            foreach (var row in StartUnitRows(tribe, unitRows)) units.Add(row.Name);
            if (units.Count > 0) sb.Append(" / ").Append(string.Join(",", units));
            var cond = StartCondition(tribe);
            if (cond != null && cond.Value.Rules != null && cond.Value.Rules.Length > 0) sb.Append(" / ").Append(cond.Value.Name);
            return sb.ToString();
        }
    }
}
