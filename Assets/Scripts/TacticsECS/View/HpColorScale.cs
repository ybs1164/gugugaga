using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// HP 비율(0~1)을 상태가 직관적으로 읽히는 색(초록/노랑/빨강)으로 바꿔주는 아주 작은 헬퍼.
    /// 유닛 머리 위 체력바(UnitView)와 선택 유닛 패널(BattleHud)이 같은 기준을 쓰도록 공유한다.
    /// </summary>
    public static class HpColorScale
    {
        private static readonly Color High = new Color(0.35f, 0.80f, 0.40f);
        private static readonly Color Mid = new Color(0.95f, 0.75f, 0.25f);
        private static readonly Color Low = new Color(0.90f, 0.30f, 0.25f);

        public static Color ForFraction(float fraction)
        {
            if (fraction > 0.6f) return High;
            if (fraction > 0.3f) return Mid;
            return Low;
        }
    }
}
