namespace TacticsECS
{
    /// <summary>
    /// 요새화 패시브(폴리토피아 Fortify). 순수 마커 — 자기 자신을 Execute하는 동작이 없다. 이 패시브를 가진
    /// 유닛이 자기 팀 도시 칸에 서 있으면 방어력 +1(CitySystem.CityDefenseBonus), 그 도시에 성벽(레벨 3 보상)이
    /// 있으면 +3(CitySystem.WallDefenseBonus)을 받는다. 보너스 계산은 TechEffectSystem.RefreshUnits가
    /// UnitActionQueries.Find&lt;FortifyAction&gt;로 보유 여부만 확인해 PositionalDefenseBonus 컴포넌트에 넣는다.
    /// </summary>
    [System.Serializable]
    public class FortifyAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Fortify;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
