using System;

namespace TacticsECS
{
    /// <summary>정보 패널(BattleHud.ShowUnit/ShowTile)의 행동 한 줄, 또는 대화 상자(BattleHud.ShowDialog)의 버튼 하나.
    /// 표시할 값과 누르면 할 일만 담는다 — 무엇을 할 수 있는지는 BattleController가 System에 물어 채운다.</summary>
    public struct ActionMenuOption
    {
        public string Label;
        public string Detail;
        public bool Enabled;
        /// <summary>대화 상자에서 강조할 기본 버튼.</summary>
        public bool Primary;
        public Action OnClick;
    }
}
