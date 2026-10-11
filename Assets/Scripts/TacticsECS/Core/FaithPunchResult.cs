using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>신앙 펀치 한 번의 결과(FaithPunchSystem.Apply가 채운다). 화면 갱신·로그용 값 묶음.</summary>
    public struct FaithPunchResult
    {
        public bool Success;

        /// <summary>효과를 받은 유닛(피해·회복).</summary>
        public List<int> UnitIds;

        /// <summary>효과를 받은 칸(자원 생성·지형 변화).</summary>
        public List<Vector2Int> Tiles;

        public bool TerrainChanged;
        public int Spent;
    }
}
