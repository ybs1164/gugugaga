using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// CSV 텍스트 <-> BiomeCsvRow 목록 변환만 담당하는 순수 파서/작성기(Unity 오브젝트를 몰라서 배치모드 CLI에서도 왕복 검증 가능).
    ///
    /// 형식(CLAUDE.md 규칙 6 — 한 칸에 한 값): 한 행이 바이옴 하나, 또는 그 바이옴의 타일 엔트리 하나, 또는 구조물 엔트리 하나인
    /// "롱 포맷" 표다. Kind 컬럼(Biome/Tile/Structure)이 행 종류, Biome 컬럼이 소속 바이옴 Id다. 행 종류마다 쓰지 않는 컬럼은 비운다.
    ///   - Biome 행: Name, NoiseType, Frequency, Octaves, SeedOffset, InnerRadius, MountainRate, ForestRate
    ///   - Tile 행: Entry(TileId), TerrainType, InnerWeight, OuterWeight, MinCount, CountPerTiles, MinDistance, EdgeMargin, Exclude1..N
    ///   - Structure 행: Entry(StructureId), AllowedTile1..N, Weight, MinCount, CountPerTiles, MinDistance, EdgeMargin,
    ///     MaxDistanceFromCity, MaxWaterFractionOnLakes(비우면 제약 없음), FillRemaining, Exclude1..N, InnerRate, OuterRate
    /// 여러 값(ExcludeAdjacent/AllowedTileTypes)은 번호 붙은 반복 컬럼으로 나열한다. 필드 의미는 BiomeCsvRow.cs 주석 참고.
    ///
    /// 옛 형식(Tiles/Structures 한 칸에 ";" 엔트리, ":" 필드, "|" 목록을 묶은 한 행 = 한 바이옴)도 헤더에 Tiles 컬럼이 있으면
    /// 호환용으로 그대로 읽는다(ParseLegacy). 작성기는 항상 새 형식으로 쓴다.
    /// </summary>
    public static class BiomeCsvSerializer
    {
        public const string KindBiome = "Biome";
        public const string KindTile = "Tile";
        public const string KindStructure = "Structure";

        private const char TileEntrySeparator = ';';
        private const char TileFieldSeparator = ':';
        private const char ExcludeSeparator = '|';

        private static readonly string[] FixedHeader =
        {
            "Kind", "Biome", "Name", "NoiseType", "Frequency", "Octaves", "SeedOffset", "InnerRadius", "MountainRate", "ForestRate",
            "Entry", "TerrainType", "InnerWeight", "OuterWeight", "Weight", "MinCount", "CountPerTiles", "MinDistance", "EdgeMargin",
            "MaxDistanceFromCity", "MaxWaterFractionOnLakes", "FillRemaining", "InnerRate", "OuterRate"
        };

        /// <summary>헤더 이름 기반으로 읽는다(CsvTableReader — 순서 무관, # 주석 행 허용). Tile/Structure 행은 같은 Biome Id의 바이옴에
        /// 붙는다(Biome 행이 없으면 기본값 바이옴을 만든다). 값이 비어있거나 형식이 잘못된 칸은 그 타입의 기본값으로 채운다(손으로
        /// 편집하다 실수해도 예외 없이 불러와지게 — 잘못된 칸은 errors를 주면 기록된다).</summary>
        public static List<BiomeCsvRow> Parse(string csvText, List<string> errors = null, string name = "biomes.csv")
        {
            if (string.IsNullOrWhiteSpace(csvText)) return new List<BiomeCsvRow>();
            var t = CsvTableReader.Parse(name, csvText);
            if (CsvTableReader.HasColumn(t, "Tiles")) return ParseLegacy(csvText);

            var rows = new List<BiomeCsvRow>();
            BiomeCsvRow Find(string id)
            {
                var b = rows.Find(x => x.Id == id);
                if (b == null) { b = new BiomeCsvRow { Id = id, Name = id }; rows.Add(b); }
                return b;
            }

            for (int r = 0; r < t.Rows.Count; r++)
            {
                string kind = CsvTableReader.Get(t, r, "Kind", KindBiome);
                string biomeId = CsvTableReader.Get(t, r, "Biome");
                if (biomeId.Length == 0) { CsvTableReader.Report(t, r, "Biome", "바이옴 Id가 비어 있음", errors); continue; }
                var biome = Find(biomeId);
                if (string.Equals(kind, KindBiome, StringComparison.OrdinalIgnoreCase))
                {
                    biome.Name = LocalizationSystem.Name("Biome", biomeId, CsvTableReader.Get(t, r, "Name"));
                    biome.NoiseType = CsvTableReader.Get(t, r, "NoiseType", "Perlin");
                    biome.Frequency = CsvTableReader.GetFloat(t, r, "Frequency", 0f, errors);
                    biome.Octaves = CsvTableReader.GetInt(t, r, "Octaves", 0, errors);
                    biome.SeedOffset = CsvTableReader.GetInt(t, r, "SeedOffset", 0, errors);
                    biome.InnerRadius = CsvTableReader.GetInt(t, r, "InnerRadius", 0, errors);
                    biome.MountainRate = CsvTableReader.GetFloat(t, r, "MountainRate", 0f, errors);
                    biome.ForestRate = CsvTableReader.GetFloat(t, r, "ForestRate", 0f, errors);
                }
                else if (string.Equals(kind, KindTile, StringComparison.OrdinalIgnoreCase))
                {
                    biome.Tiles.Add(new BiomeTileEntry
                    {
                        TileId = CsvTableReader.Get(t, r, "Entry"),
                        TerrainType = CsvTableReader.GetEnum(t, r, "TerrainType", TerrainType.Land, errors),
                        InnerWeight = CsvTableReader.GetFloat(t, r, "InnerWeight", 0f, errors),
                        OuterWeight = CsvTableReader.GetFloat(t, r, "OuterWeight", 0f, errors),
                        MinCount = CsvTableReader.GetInt(t, r, "MinCount", 0, errors),
                        CountPerTiles = CsvTableReader.GetFloat(t, r, "CountPerTiles", 0f, errors),
                        MinDistance = CsvTableReader.GetInt(t, r, "MinDistance", 0, errors),
                        EdgeMargin = CsvTableReader.GetInt(t, r, "EdgeMargin", 0, errors),
                        ExcludeAdjacent = CsvTableReader.GetList(t, r, "Exclude"),
                    });
                }
                else if (string.Equals(kind, KindStructure, StringComparison.OrdinalIgnoreCase))
                {
                    string lakes = CsvTableReader.Get(t, r, "MaxWaterFractionOnLakes");
                    biome.Structures.Add(new BiomeStructureEntry
                    {
                        StructureId = CsvTableReader.Get(t, r, "Entry"),
                        AllowedTileTypes = CsvTableReader.GetList(t, r, "AllowedTile"),
                        Weight = CsvTableReader.GetFloat(t, r, "Weight", 0f, errors),
                        MinCount = CsvTableReader.GetInt(t, r, "MinCount", 0, errors),
                        CountPerTiles = CsvTableReader.GetFloat(t, r, "CountPerTiles", 0f, errors),
                        MinDistance = CsvTableReader.GetInt(t, r, "MinDistance", 0, errors),
                        EdgeMargin = CsvTableReader.GetInt(t, r, "EdgeMargin", 0, errors),
                        MaxDistanceFromCity = CsvTableReader.GetInt(t, r, "MaxDistanceFromCity", 0, errors),
                        MaxWaterFractionOnLakes = lakes.Length == 0 ? (float?)null : CsvTableReader.GetFloat(t, r, "MaxWaterFractionOnLakes", 0f, errors),
                        FillRemaining = CsvTableReader.GetBool(t, r, "FillRemaining", false, errors),
                        ExcludeAdjacentStructures = CsvTableReader.GetList(t, r, "Exclude"),
                        InnerRate = CsvTableReader.GetFloat(t, r, "InnerRate", 0f, errors),
                        OuterRate = CsvTableReader.GetFloat(t, r, "OuterRate", 0f, errors),
                    });
                }
                else CsvTableReader.Report(t, r, "Kind", $"알 수 없는 행 종류 '{kind}' (가능: {KindBiome}/{KindTile}/{KindStructure})", errors);
            }
            return rows;
        }

        public static string Write(IReadOnlyList<BiomeCsvRow> rows)
        {
            int excludeCount = 1, allowedCount = 1;
            foreach (var b in rows)
            {
                foreach (var e in b.Tiles) excludeCount = Math.Max(excludeCount, e.ExcludeAdjacent?.Length ?? 0);
                foreach (var e in b.Structures)
                {
                    excludeCount = Math.Max(excludeCount, e.ExcludeAdjacentStructures?.Length ?? 0);
                    allowedCount = Math.Max(allowedCount, e.AllowedTileTypes?.Length ?? 0);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", FixedHeader.Concat(CsvTableReader.ListHeader("AllowedTile", allowedCount)).Concat(CsvTableReader.ListHeader("Exclude", excludeCount))));
            void Line(string[] fixedCells, IReadOnlyList<string> allowed, IReadOnlyList<string> exclude)
            {
                var cells = new List<string>(fixedCells.Select(CsvTableReader.Quote));
                while (cells.Count < FixedHeader.Length) cells.Add(string.Empty);
                cells.AddRange(CsvTableReader.ListCells(allowed, allowedCount));
                cells.AddRange(CsvTableReader.ListCells(exclude, excludeCount));
                sb.AppendLine(string.Join(",", cells));
            }

            foreach (var b in rows)
            {
                Line(new[]
                {
                    KindBiome, b.Id ?? string.Empty, b.Name ?? string.Empty, b.NoiseType ?? "Perlin", F(b.Frequency), I(b.Octaves), I(b.SeedOffset),
                    I(b.InnerRadius), F(b.MountainRate), F(b.ForestRate)
                }, null, null);
                foreach (var e in b.Tiles)
                    Line(new[]
                    {
                        KindTile, b.Id ?? string.Empty, "", "", "", "", "", "", "", "",
                        e.TileId ?? string.Empty, e.TerrainType.ToString(), F(e.InnerWeight), F(e.OuterWeight), "", I(e.MinCount), F(e.CountPerTiles),
                        I(e.MinDistance), I(e.EdgeMargin)
                    }, null, e.ExcludeAdjacent);
                foreach (var e in b.Structures)
                    Line(new[]
                    {
                        KindStructure, b.Id ?? string.Empty, "", "", "", "", "", "", "", "",
                        e.StructureId ?? string.Empty, "", "", "", F(e.Weight), I(e.MinCount), F(e.CountPerTiles), I(e.MinDistance), I(e.EdgeMargin),
                        I(e.MaxDistanceFromCity), e.MaxWaterFractionOnLakes.HasValue ? F(e.MaxWaterFractionOnLakes.Value) : string.Empty,
                        e.FillRemaining ? "1" : "0", F(e.InnerRate), F(e.OuterRate)
                    }, e.AllowedTileTypes, e.ExcludeAdjacentStructures);
            }
            return sb.ToString();
        }

        private static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        // ---------- 옛 형식(한 행 = 한 바이옴, Tiles/Structures 칸에 묶음) 읽기 — 호환용 ----------

        /// <summary>옛 형식: Id,Name,NoiseType,Frequency,Octaves,SeedOffset,InnerRadius,Tiles,Structures,MountainRate,ForestRate.
        /// 타일 엔트리 TileId:TerrainType:InnerWeight:OuterWeight:MinCount:CountPerTiles:MinDistance:EdgeMargin:ExcludeAdjacent(파이프),
        /// 구조물 엔트리 StructureId:AllowedTileTypes(파이프):Weight:MinCount:CountPerTiles:MinDistance:EdgeMargin:MaxDistanceFromCity:
        /// MaxWaterFractionOnLakes:FillRemaining:ExcludeAdjacentStructures(파이프):InnerRate:OuterRate.</summary>
        public static List<BiomeCsvRow> ParseLegacy(string csvText)
        {
            var rows = new List<BiomeCsvRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            var lines = csvText.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                rows.Add(ParseRow(line.Split(',')));
            }
            return rows;
        }

        private static BiomeCsvRow ParseRow(string[] c) => new BiomeCsvRow
        {
            Id = Col(c, 0),
            Name = Col(c, 1),
            NoiseType = string.IsNullOrEmpty(Col(c, 2)) ? "Perlin" : Col(c, 2),
            Frequency = ParseFloat(Col(c, 3)),
            Octaves = ParseInt(Col(c, 4)),
            SeedOffset = ParseInt(Col(c, 5)),
            InnerRadius = ParseInt(Col(c, 6)),
            Tiles = ParseTiles(Col(c, 7)),
            Structures = ParseStructures(Col(c, 8)),
            MountainRate = ParseFloat(Col(c, 9)),
            ForestRate = ParseFloat(Col(c, 10))
        };

        private static List<BiomeTileEntry> ParseTiles(string s)
        {
            var result = new List<BiomeTileEntry>();
            if (string.IsNullOrEmpty(s)) return result;

            foreach (var token in s.Split(TileEntrySeparator))
            {
                var entry = token.Trim();
                if (entry.Length == 0) continue;

                var f = entry.Split(TileFieldSeparator);
                result.Add(new BiomeTileEntry
                {
                    TileId = FieldCol(f, 0),
                    TerrainType = ParseTerrainType(FieldCol(f, 1)),
                    InnerWeight = ParseFloat(FieldCol(f, 2)),
                    OuterWeight = ParseFloat(FieldCol(f, 3)),
                    MinCount = ParseInt(FieldCol(f, 4)),
                    CountPerTiles = ParseFloat(FieldCol(f, 5)),
                    MinDistance = ParseInt(FieldCol(f, 6)),
                    EdgeMargin = ParseInt(FieldCol(f, 7)),
                    ExcludeAdjacent = ParseExclude(FieldCol(f, 8))
                });
            }
            return result;
        }

        private static List<BiomeStructureEntry> ParseStructures(string s)
        {
            var result = new List<BiomeStructureEntry>();
            if (string.IsNullOrEmpty(s)) return result;

            foreach (var token in s.Split(TileEntrySeparator))
            {
                var entry = token.Trim();
                if (entry.Length == 0) continue;

                var f = entry.Split(TileFieldSeparator);
                result.Add(new BiomeStructureEntry
                {
                    StructureId = FieldCol(f, 0),
                    AllowedTileTypes = ParseExclude(FieldCol(f, 1)),
                    Weight = ParseFloat(FieldCol(f, 2)),
                    MinCount = ParseInt(FieldCol(f, 3)),
                    CountPerTiles = ParseFloat(FieldCol(f, 4)),
                    MinDistance = ParseInt(FieldCol(f, 5)),
                    EdgeMargin = ParseInt(FieldCol(f, 6)),
                    MaxDistanceFromCity = ParseInt(FieldCol(f, 7)),
                    MaxWaterFractionOnLakes = ParseFloatOrNull(FieldCol(f, 8)),
                    FillRemaining = ParseInt(FieldCol(f, 9)) != 0,
                    ExcludeAdjacentStructures = ParseExclude(FieldCol(f, 10)),
                    InnerRate = ParseFloat(FieldCol(f, 11)),
                    OuterRate = ParseFloat(FieldCol(f, 12))
                });
            }
            return result;
        }

        private static string[] ParseExclude(string s) =>
            string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(ExcludeSeparator);

        private static TerrainType ParseTerrainType(string s) =>
            Enum.TryParse<TerrainType>(s, true, out var v) ? v : TerrainType.Land;

        private static string Col(string[] cols, int index) => index < cols.Length ? cols[index].Trim() : string.Empty;
        private static string FieldCol(string[] fields, int index) => index < fields.Length ? fields[index].Trim() : string.Empty;

        private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        /// <summary>MaxWaterFractionOnLakes처럼 "비워두면 제약 없음"을 null로 표현하는 필드용 — 빈 문자열이면
        /// null, 숫자면 그 값을 쓴다.</summary>
        private static float? ParseFloatOrNull(string s) =>
            string.IsNullOrEmpty(s) ? (float?)null : ParseFloat(s);
    }
}
