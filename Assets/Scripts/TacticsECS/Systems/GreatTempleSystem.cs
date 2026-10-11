using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 승리(대신전). 자체 상태 없이 EconomyWorld.GreatTemples를 읽고 쓴다.
    /// 건설(과업 달성 + 자기 영토의 이어진 빈 평지 N칸) → 보호 기간(인접 칸에 적이 들어오면 다시 센다) → 타이브레이크 대기 →
    /// 유일한 대신전이면 즉시 승리(EconomyWorld.FaithWinner). 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class GreatTempleSystem
    {
        public const string BuildingId = "GreatTemple";
        public const string TaskId = "GreatTemple";
        private const string NaturalResourcePrefix = "Resource_";

        public static BuildingInfo? Info() => TileImprovementSystem.FindBuilding(BuildingId);

        public static int IndexOf(EconomyWorld econ, Team team)
        {
            for (int i = 0; i < econ.GreatTemples.Count; i++)
                if (econ.GreatTemples[i].Owner == team) return i;
            return -1;
        }

        // ---------- 건설 ----------

        /// <summary>origin에서 이어진(4방향) 자기 영토의 지을 수 있는 칸(빈 평지, 숲, 자원 칸) N개. 모자라면 null.</summary>
        public static List<Vector2Int> FindSite(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, Vector2Int origin)
        {
            int need = GameRules.Faith.GreatTempleMinTiles;
            var site = new List<Vector2Int>();
            if (!IsFree(grid, econ, team, origin)) return null;
            var seen = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);
            while (queue.Count > 0 && site.Count < need)
            {
                var p = queue.Dequeue();
                site.Add(p);
                foreach (var d in GridWorld.Dir4)
                {
                    var n = p + d;
                    if (seen.Contains(n) || !IsFree(grid, econ, team, n)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return site.Count >= need ? site : null;
        }

        public static bool IsFree(GridWorld grid, EconomyWorld econ, Team team, Vector2Int p)
        {
            if (!grid.InBounds(p)) return false;
            var tile = grid.GetTile(p);
            if (tile.OwnerTeam != (int)team || tile.OccupantId != TileData.NoOccupant) return false;
            var cls = TileImprovementSystem.Classify(grid, p);
            if (cls != TileClass.Field && cls != TileClass.Forest) return false; // 숲은 지을 때 치워진다
            if (!string.IsNullOrEmpty(tile.BuildingId) || tile.HasRoad) return false;
            if (!string.IsNullOrEmpty(tile.StructureId) && !tile.StructureId.StartsWith(NaturalResourcePrefix)) return false; // 도시·유적·등대 등은 안 된다. 자원은 치워진다
            return CitySystem.FindCityAt(econ, p) < 0;
        }

        /// <summary>자기 영토에서 대신전을 지을 수 있는 칸(첫 번째), 없으면 null.</summary>
        public static Vector2Int? AnySite(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (grid.GetTile(p).OwnerTeam == (int)team && FindSite(grid, world, econ, team, p) != null) return p;
            }
            return null;
        }

        /// <summary>지을 수 없으면 이유 문구 키, 지을 수 있으면 null.</summary>
        public static string CanBuild(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, Vector2Int origin)
        {
            var info = Info();
            if (info == null) return "UI.GreatTemple.NoSite";
            if (!econ.Tasks.TryGetValue(team, out var p) || !p.Completed.Contains(TaskId)) return "UI.GreatTemple.NeedTask";
            if (IndexOf(econ, team) >= 0) return "UI.GreatTemple.Exists";
            if (econ.Resources[team].Stars < info.Value.Cost) return "UI.GreatTemple.NeedStars";
            return FindSite(grid, world, econ, team, origin) == null ? "UI.GreatTemple.NoSite" : null;
        }

        /// <summary>대신전을 짓는다(별 소모, 칸마다 건물 표시, 모든 팀에게 위치 공개). CanBuild가 null이어야 한다.</summary>
        public static bool Build(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, Vector2Int origin, int turn)
        {
            if (CanBuild(grid, world, econ, team, origin) != null) return false;
            var site = FindSite(grid, world, econ, team, origin);
            var res = econ.Resources[team];
            res.Stars -= Info().Value.Cost;
            econ.Resources[team] = res;

            int everyone = 0;
            foreach (var t in CitySystem.Teams) everyone |= 1 << (int)t;
            foreach (var pos in site)
            {
                var tile = grid.GetTile(pos);
                if (TileImprovementSystem.Classify(grid, pos) == TileClass.Forest) tile.TileTypeId = TileImprovementSystem.NeighborFieldTileType(grid, pos); // 숲을 치운다
                tile.StructureId = string.Empty; // 자원도 치운다
                tile.BuildingId = BuildingId;
                tile.BuildingTurn = turn;
                tile.ExploredMask |= everyone; // 세션의 모든 부족에게 위치 공개
                grid.SetTile(pos, tile);
            }
            econ.GreatTemples.Add(new GreatTempleData { Owner = team, Tiles = site.ToArray(), State = GreatTempleState.Building, StartTurn = turn, CompleteTurn = -1 });
            return true;
        }

        // ---------- 보호 / 타이브레이크 / 승리 ----------

        private static bool EnemyAdjacent(GridWorld grid, EntityWorld world, GreatTempleData temple)
        {
            foreach (var pos in temple.Tiles)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var p = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (!grid.InBounds(p)) continue;
                    int id = grid.GetOccupant(p);
                    if (id != TileData.NoOccupant && UnitQueries.IsAlive(world, id) && world.Get<Team>(id) != temple.Owner) return true;
                }
            return false;
        }

        /// <summary>상태가 바뀔 때마다 부른다: 보호 중인 대신전 인접 칸에 적이 있으면 보호 기간을 지금부터 다시 센다.</summary>
        public static void Refresh(GridWorld grid, EntityWorld world, EconomyWorld econ, int turn)
        {
            for (int i = 0; i < econ.GreatTemples.Count; i++)
            {
                var t = econ.GreatTemples[i];
                if (t.State != GreatTempleState.Building || !EnemyAdjacent(grid, world, t)) continue;
                t.StartTurn = turn;
                econ.GreatTemples[i] = t;
            }
        }

        /// <summary>다른 부족의 대신전이 있고 그 부족이 아직 살아 있으면 true(타이브레이크가 걸린다).</summary>
        private static bool HasRival(EntityWorld world, EconomyWorld econ, Team team)
        {
            foreach (var t in econ.GreatTemples)
                if (t.Owner != team && UnitQueries.AnyAlive(world, t.Owner)) return true;
            return false;
        }

        /// <summary>그 팀 턴 시작: 보호 기간을 채우면 완성(타이브레이크 대기), 대기를 마치고 유일한 대신전이면 승리. 승리 팀을 econ.FaithWinner에 적는다.</summary>
        public static void OnTurnStart(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turn)
        {
            Refresh(grid, world, econ, turn);
            for (int i = 0; i < econ.GreatTemples.Count; i++)
            {
                var t = econ.GreatTemples[i];
                if (t.Owner != team) continue;
                if (t.State == GreatTempleState.Building && turn - t.StartTurn >= GameRules.Faith.GreatTempleProtectTurns)
                {
                    t.State = GreatTempleState.Waiting;
                    t.CompleteTurn = turn;
                    econ.GreatTemples[i] = t;
                }
                if (t.State == GreatTempleState.Waiting && turn - t.CompleteTurn >= GameRules.Faith.GreatTempleTieBreakTurns
                    && !HasRival(world, econ, team) && econ.FaithWinner < 0)
                    econ.FaithWinner = (int)team;
            }
        }

        // ---------- 표시용 ----------

        /// <summary>HUD 상태 줄: 내 대신전의 진행과 상대 대신전 소식. 없으면 빈 문자열.</summary>
        public static string StatusLine(EntityWorld world, EconomyWorld econ, Team viewer, int turn)
        {
            var lines = new List<string>();
            foreach (var t in econ.GreatTemples)
            {
                if (t.Owner == viewer)
                {
                    if (t.State == GreatTempleState.Building)
                        lines.Add(LocalizationSystem.F("UI.GreatTemple.StatusBuilding", Mathf.Clamp(turn - t.StartTurn, 0, GameRules.Faith.GreatTempleProtectTurns), GameRules.Faith.GreatTempleProtectTurns));
                    else
                        lines.Add(LocalizationSystem.F(HasRival(world, econ, viewer) ? "UI.GreatTemple.StatusSuspended" : "UI.GreatTemple.StatusWaiting",
                            Mathf.Max(0, GameRules.Faith.GreatTempleTieBreakTurns - (turn - t.CompleteTurn))));
                }
                else lines.Add(LocalizationSystem.F("UI.GreatTemple.StatusRival", t.Tiles[0].x, t.Tiles[0].y));
            }
            return string.Join("\n", lines);
        }
    }
}
