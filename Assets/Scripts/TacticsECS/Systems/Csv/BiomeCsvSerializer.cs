using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// CSV 텍스트 <-> BiomeCsvRow 목록 변환만 담당하는 순수 파서/작성기. UnitCsvSerializer와 완전히 같은
    /// 패턴이다(Unity 오브젝트를 몰라서 배치모드 CLI에서도 왕복 검증 가능, 컬럼 순서는 Header 한 곳에만).
    /// Tiles 컬럼 하나에 이 바이옴의 타일 엔트리 전부를 담아야 해서 구분자를 3단계로 나눈다 — 세미콜론(;)
    /// = 엔트리 구분(UnitCsvSerializer의 Actions 컬럼과 동일), 콜론(:) = 엔트리 안의 필드 구분, 파이프(|) =
    /// ExcludeAdjacent 안의 타일 이름 여러 개 구분. 콤마(CSV 컬럼 구분자)를 전혀 쓰지 않으므로 따옴표 CSV를
    /// 지원하지 않는 단순 Split(',') 파서로도 안전하게 왕복된다.
    /// </summary>
    public static class BiomeCsvSerializer
    {
        private static readonly string[] Header = { "Id", "Name", "NoiseType", "Frequency", "Octaves", "SeedOffset", "Tiles" };

        private const char TileEntrySeparator = ';';
        private const char TileFieldSeparator = ':';
        private const char ExcludeSeparator = '|';

        /// <summary>첫 줄(헤더)을 건너뛰고 나머지 줄을 각각 한 행으로 파싱한다. 비어있는 줄은 무시한다.
        /// 값이 비어있거나 형식이 잘못된 컬럼은 그 타입의 기본값으로 채운다 — UnitCsvSerializer.Parse와
        /// 같은 이유(손으로 편집하다 실수해도 예외 없이 불러와지게).</summary>
        public static List<BiomeCsvRow> Parse(string csvText)
        {
            var rows = new List<BiomeCsvRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            var lines = csvText.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                rows.Add(ParseRow(line.Split(',')));
            }
            return rows;
        }

        public static string Write(IReadOnlyList<BiomeCsvRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header));
            foreach (var row in rows)
                sb.AppendLine(WriteRow(row));
            return sb.ToString();
        }

        private static BiomeCsvRow ParseRow(string[] c) => new BiomeCsvRow
        {
            Id = Col(c, 0),
            Name = Col(c, 1),
            NoiseType = string.IsNullOrEmpty(Col(c, 2)) ? "Perlin" : Col(c, 2),
            Frequency = ParseFloat(Col(c, 3)),
            Octaves = ParseInt(Col(c, 4)),
            SeedOffset = ParseInt(Col(c, 5)),
            Tiles = ParseTiles(Col(c, 6))
        };

        private static string WriteRow(BiomeCsvRow row) => string.Join(",", new[]
        {
            row.Id ?? string.Empty,
            row.Name ?? string.Empty,
            row.NoiseType ?? "Perlin",
            row.Frequency.ToString(CultureInfo.InvariantCulture),
            row.Octaves.ToString(CultureInfo.InvariantCulture),
            row.SeedOffset.ToString(CultureInfo.InvariantCulture),
            WriteTiles(row.Tiles)
        });

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
                    Weight = ParseFloat(FieldCol(f, 2)),
                    MinCount = ParseInt(FieldCol(f, 3)),
                    ExcludeAdjacent = ParseExclude(FieldCol(f, 4))
                });
            }
            return result;
        }

        private static string WriteTiles(List<BiomeTileEntry> tiles)
        {
            if (tiles == null || tiles.Count == 0) return string.Empty;
            return string.Join(TileEntrySeparator.ToString(), tiles.Select(WriteTileEntry));
        }

        private static string WriteTileEntry(BiomeTileEntry entry) => string.Join(TileFieldSeparator.ToString(), new[]
        {
            entry.TileId ?? string.Empty,
            entry.TerrainType.ToString(),
            entry.Weight.ToString(CultureInfo.InvariantCulture),
            entry.MinCount.ToString(CultureInfo.InvariantCulture),
            entry.ExcludeAdjacent == null ? string.Empty : string.Join(ExcludeSeparator.ToString(), entry.ExcludeAdjacent)
        });

        private static string[] ParseExclude(string s) =>
            string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(ExcludeSeparator);

        private static TerrainType ParseTerrainType(string s) =>
            Enum.TryParse<TerrainType>(s, true, out var v) ? v : TerrainType.Land;

        private static string Col(string[] cols, int index) => index < cols.Length ? cols[index].Trim() : string.Empty;
        private static string FieldCol(string[] fields, int index) => index < fields.Length ? fields[index].Trim() : string.Empty;

        private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }
}
