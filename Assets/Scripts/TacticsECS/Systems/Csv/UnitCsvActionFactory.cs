using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// UnitCsvRow(값) <-> IUnitAction 목록(행동) 변환만 담당하는 정적 클래스. 어떤 ActionType 태그가
    /// 어떤 IUnitAction 구현체·파라미터에 대응하는지 아는 곳은 여기 하나뿐이라, 새 행동이 추가되면
    /// 이 파일 두 메서드에만 한 줄씩 추가하면 된다.
    /// </summary>
    public static class UnitCsvActionFactory
    {
        /// <summary>불러오기: CSV 행의 Actions 플래그에 있는 것만, 그 행동의 CSV 파라미터로 인스턴스를 만든다.</summary>
        public static List<IUnitAction> BuildActions(UnitCsvRow row)
        {
            var actions = new List<IUnitAction>();

            if ((row.Actions & ActionType.Move) != 0)
                actions.Add(MoveAction.FromCsv(row.MoveRange));
            if ((row.Actions & ActionType.Attack) != 0)
                actions.Add(AttackAction.FromCsv(row.AttackAttack, row.AttackRange));
            if ((row.Actions & ActionType.Defend) != 0)
                actions.Add(new DefendAction());
            if ((row.Actions & ActionType.Heal) != 0)
                actions.Add(HealAction.FromCsv(row.HealAmount, row.HealRange));
            if ((row.Actions & ActionType.SelfDestruct) != 0)
                actions.Add(new SelfDestructAction());
            if ((row.Actions & ActionType.Counter) != 0)
                actions.Add(new CounterAction());
            if ((row.Actions & ActionType.Charge) != 0)
                actions.Add(new ChargeAction());
            if ((row.Actions & ActionType.Retreat) != 0)
                actions.Add(new RetreatAction());
            if ((row.Actions & ActionType.Ambush) != 0)
                actions.Add(new AmbushAction());
            if ((row.Actions & ActionType.Infiltrate) != 0)
                actions.Add(new InfiltrateAction());
            if ((row.Actions & ActionType.Herd) != 0)
                actions.Add(new HerdAction());
            if ((row.Actions & ActionType.Convert) != 0)
                actions.Add(new ConvertAction());
            if ((row.Actions & ActionType.Combo) != 0)
                actions.Add(new ComboAction());
            if ((row.Actions & ActionType.Scout) != 0)
                actions.Add(new ScoutAction());
            if ((row.Actions & ActionType.Splash) != 0)
                actions.Add(new SplashAction());
            if ((row.Actions & ActionType.Stiff) != 0)
                actions.Add(new StiffAction());
            if ((row.Actions & ActionType.Freeze) != 0)
                actions.Add(new FreezeAction());
            if ((row.Actions & ActionType.Fortify) != 0)
                actions.Add(new FortifyAction());
            if ((row.Actions & ActionType.Stealth) != 0)
                actions.Add(new StealthAction());
            if ((row.Actions & ActionType.Pillage) != 0)
                actions.Add(new PillageAction());
            if ((row.Actions & ActionType.Anchored) != 0)
                actions.Add(new AnchoredAction());
            if ((row.Actions & ActionType.Transport) != 0)
                actions.Add(TransportAction.FromCsv(row.TransportCapacity));
            if ((row.Actions & ActionType.Wait) != 0)
                actions.Add(new WaitAction());
            if ((row.Actions & ActionType.IgnoreTerrain) != 0)
                actions.Add(new IgnoreTerrainAction());
            if ((row.Actions & ActionType.IgnoreUnitBlocking) != 0)
                actions.Add(new IgnoreUnitBlockingAction());
            if ((row.Actions & ActionType.AllowDiagonal) != 0)
                actions.Add(new AllowDiagonalAction());

            // 모든 유닛은 대기(WaitAction)를 기본 보유한다
            if (!actions.Exists(a => a is WaitAction))
                actions.Add(new WaitAction());

            return actions;
        }

        /// <summary>내보내기: 이름/스탯과 실제 행동 목록으로부터 CSV 행을 만든다. 기존 프리팹
        /// (UnitDefinition)의 현재 값을 CSV 템플릿으로 뽑아낼 때, 그리고 향후 샌드박스에서 배치된 유닛
        /// 구성을 그대로 CSV로 내보낼 때 공통으로 쓴다.</summary>
        public static UnitCsvRow ToRow(string name, string baseVisual, int maxHp, int defense,
            IReadOnlyList<IUnitAction> actions, TerrainType domain = TerrainType.Land)
        {
            var row = new UnitCsvRow
            {
                Name = name,
                MaxHp = maxHp,
                Defense = defense,
                BaseVisual = baseVisual,
                Domain = domain
            };

            foreach (var action in actions)
            {
                if (action == null) continue;
                row.Actions |= action.GetActionType();

                switch (action)
                {
                    case MoveAction move:
                        row.MoveRange = move.MoveRange;
                        break;
                    case AttackAction attack:
                        row.AttackAttack = attack.Attack;
                        row.AttackRange = attack.AttackRange;
                        break;
                    case HealAction heal:
                        row.HealAmount = heal.HealAmount;
                        row.HealRange = heal.HealRange;
                        break;
                    case TransportAction transport:
                        row.TransportCapacity = transport.Capacity;
                        break;
                }
            }

            return row;
        }
    }
}
