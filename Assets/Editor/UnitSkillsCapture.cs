using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 유닛 스킬 화면 점검 캡처(에디트 모드, Play Mode 없음): Sandbox 씬에서 전투를 시작하고 수도 옆에 상태를 직접 만든 뒤 찍는다 —
    /// 숨은 적 옆 (!) 표시, 중독 배지, 얼음·침수 바닥, 유닛 메뉴의 침수·얼음 깨기·상륙 버튼, 약탈·폭우 공격 칸.
    /// 규칙: docs/spec/game/unit.md, vision.md#은신, tile.md#침수와-얼음.
    /// 사용법(화면 필요, -nographics 금지): unity run . -- -logFile Logs/unit_skills_capture.log -executeMethod TacticsECS.EditorTools.UnitSkillsCapture.Run
    /// 결과: Logs/unit_skills_capture/*.png
    /// </summary>
    public static class UnitSkillsCapture
    {
        private const string OutDir = "Logs/unit_skills_capture";
        private static BattleController _c;
        private static GridWorld _grid;
        private static EntityWorld _world;
        private static EconomyWorld _econ;

        private static T Field<T>(string name) => (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);
        private static void SetField(string name, object value) => typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_c, value);
        private static object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            GameDataLoader.LoadAll();
            StartBattle();

            var anchor = Anchor();
            Prepare(anchor, 4);

            // 칸: 침수 1, 얼음 2(+물 1), 물 줄
            var flooded = anchor + new Vector2Int(0, 1);
            Land(flooded); SetFlooded(flooded);
            for (int dy = -1; dy <= 1; dy++) Water(anchor + new Vector2Int(3, dy));

            // 1) (!)·중독·얼음·침수
            int scout = Spawn(Team.Player, "infantry", anchor + new Vector2Int(-1, 0));
            int hidden = Spawn(Team.Enemy, "spy", anchor + new Vector2Int(-2, 0));
            _world.Set(hidden, new Hidden { Value = true });
            int poisoned = Spawn(Team.Player, "shield", anchor + new Vector2Int(0, -1));
            _world.Set(poisoned, new Poisoned { Value = true });
            _world.Set(poisoned, new Hp { Value = 9 });
            int mooni = Spawn(Team.Player, "mooni", anchor + new Vector2Int(1, 0));
            MovementSystem.TryMove(_grid, _world, mooni, anchor + new Vector2Int(2, 0)); // 옆 물이 언다
            Refresh(true);
            Focus(anchor + new Vector2Int(0, 0));
            Shot("01_alert_poison_ice_flood");

            // 2) 얼음 깨기 버튼(얼음 옆 보병)
            int breaker = Spawn(Team.Player, "infantry", anchor + new Vector2Int(2, 1));
            Refresh(true);
            Call("FocusUnit", breaker);
            Shot("02_menu_break_ice");

            // 3) 침수 버튼(침수 패시브 유닛, 평지 위)
            var floodRow = new UnitCsvRow { Id = "flooder", Name = "Flooder", MaxHp = 10, Defense = 1, BaseVisual = "Melee",
                Actions = ActionType.Move | ActionType.Attack | ActionType.Flood | ActionType.Counter, MoveRange = 1, AttackAttack = 2, AttackRange = 1 };
            _econ.UnitRows.Add(floodRow);
            int flooder = Spawn(Team.Player, floodRow, anchor + new Vector2Int(1, -1));
            Call("ClearSelection");
            Call("FocusUnit", flooder);
            Shot("03_menu_flood");

            // 4) 상륙 버튼(침수 칸 위 뗏목)
            int sailor = Spawn(Team.Player, "infantry", flooded);
            EmbarkSystem.EmbarkAs(_world, sailor, NavalUnitDefinition.RaftId);
            Refresh(false);
            Call("ClearSelection");
            Call("FocusUnit", sailor);
            Shot("04_menu_disembark");

            // 5) 폭우 공격 칸(복어 — 빈 땅도 빨갛게)
            Water(anchor + new Vector2Int(4, 1));
            int puffer = Spawn(Team.Player, "puffer", anchor + new Vector2Int(4, 1));
            Call("ClearSelection");
            Call("FocusUnit", puffer);
            Shot("05_drench_targets");

            // 6) 약탈 공격 칸(적 도시 옆 스파이)
            Call("ClearSelection");
            int enemyCity = CitySystem.FindCapital(_econ, Team.Enemy);
            var enemyPos = _econ.Cities[enemyCity].Position;
            VisionSystem.Reveal(_grid, Team.Player, enemyPos, 3);
            var spot = FreeNear(enemyPos);
            int spy = Spawn(Team.Player, "spy", spot);
            Refresh(true);
            Call("FocusUnit", spy);
            Focus(enemyPos);
            Shot("06_plunder_target");

            Call("ClearSelection");
            Debug.Log("[UnitSkillsCapture] DONE");
        }

        private static void StartBattle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            _c = Object.FindFirstObjectByType<BattleController>();
            Call("Awake"); Call("SetupBattle");
            Call("HandleGenerateTerrain");
            SetField("_unitRows", new List<UnitCsvRow>(GameTables.Units));
            SetField("_placementActive", false);
            var sandboxHud = Field<SandboxHud>("_sandboxHud");
            if (sandboxHud != null) Object.DestroyImmediate(sandboxHud.gameObject);
            SetField("_sandboxHud", null);
            SetField("_placementController", null);
            Call("SetBattleOnlyHudVisible", true);
            Call("BeginBattle");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) PixelUIScaler.Attach(canvas.gameObject);
            _econ = Field<EconomyWorld>("_econ");
            _world = Field<EntityWorld>("_world");
            _grid = Field<GridWorld>("_grid");
        }

        /// <summary>플레이어 수도에서 맵 안쪽으로 4칸 — 이 둘레(반경 4)를 장면으로 쓴다.</summary>
        private static Vector2Int Anchor()
        {
            var cap = _econ.Cities[CitySystem.FindCapital(_econ, Team.Player)].Position;
            int dx = cap.x < _grid.Width / 2 ? 4 : -4, dy = cap.y < _grid.Height / 2 ? 2 : -2;
            return new Vector2Int(Mathf.Clamp(cap.x + dx, 4, _grid.Width - 5), Mathf.Clamp(cap.y + dy, 4, _grid.Height - 5));
        }

        /// <summary>장면 칸을 비운다: 유닛 제거, 평지·밝힘, 건물·구조물 없음(도시 칸은 그대로).</summary>
        private static void Prepare(Vector2Int center, int radius)
        {
            VisionSystem.Reveal(_grid, Team.Player, center, radius + 1);
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var p = center + new Vector2Int(dx, dy);
                if (!_grid.InBounds(p) || CitySystem.IsSettlementTile(_grid, p)) continue;
                int occ = _grid.GetOccupant(p);
                if (occ != TileData.NoOccupant) { _world.Set(occ, new Hp { Value = 0 }); _grid.RemoveOccupant(p); }
                Land(p);
            }
        }

        private static void Land(Vector2Int p)
        {
            var t = _grid.GetTile(p);
            t.Terrain = TerrainType.Land; t.Walkable = true; t.TileTypeId = TileImprovementSystem.NeighborFieldTileType(_grid, p);
            if (string.IsNullOrEmpty(t.TileTypeId)) t.TileTypeId = "Grass";
            t.BuildingId = string.Empty; t.StructureId = string.Empty; t.Flooded = false; t.Iced = false;
            _grid.SetTile(p, t);
        }

        private static void Water(Vector2Int p)
        {
            var t = _grid.GetTile(p);
            t.Terrain = TerrainType.Water; t.TileTypeId = "Water"; t.Iced = false; t.Flooded = false;
            _grid.SetTile(p, t);
        }

        private static void SetFlooded(Vector2Int p)
        {
            var t = _grid.GetTile(p); t.Flooded = true; _grid.SetTile(p, t);
        }

        private static int Spawn(Team team, string id, Vector2Int p) => Spawn(team, GameTables.Units, id, p);

        private static int Spawn(Team team, IEnumerable<UnitCsvRow> rows, string id, Vector2Int p)
        {
            foreach (var r in rows) if (r.Id == id) return Spawn(team, r, p);
            throw new System.ArgumentException(id);
        }

        private static int Spawn(Team team, UnitCsvRow row, Vector2Int p)
        {
            int occ = _grid.GetOccupant(p);
            if (occ != TileData.NoOccupant) { _world.Set(occ, new Hp { Value = 0 }); _grid.RemoveOccupant(p); }
            int id = UnitFactorySystem.CreateFromCsv(_grid, _world, team, row, p);
            Call("AttachSpawnedView", id);
            return id;
        }

        private static Vector2Int FreeNear(Vector2Int center)
        {
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var p = center + d;
                if (_grid.InBounds(p) && _grid.GetTerrain(p) == TerrainType.Land && !_grid.IsOccupied(p)) return p;
            }
            var q = center + Vector2Int.right;
            Land(q);
            return q;
        }

        private static void Refresh(bool terrain)
        {
            Call("RefreshEconomyViews", terrain);
            Call("RefreshAllViews");
        }

        private static void Focus(Vector2Int p)
        {
            var cam = Camera.main.GetComponent<PixelCamera>();
            var world = PixelCoordinates.GridToWorld(_grid, p);
            cam.RequestedPosition = new Vector3(world.x, world.y, cam.transform.position.z);
            cam.RequestedSize = 3.5f;
            cam.Apply();
        }

        private static void Shot(string name)
        {
            Call("RefreshAllViews");
            Pixel2DVerification.CaptureCamera(Camera.main, $"{OutDir}/{name}.png", 1600, 900, true);
        }
    }
}
