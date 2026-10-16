using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이어하기에 필요한 한 판 전체(SaveSystem이 파일로 쓰고 읽는다). 게임 상태는 데이터 세 덩어리(GridWorld·EntityWorld·EconomyWorld)와
    /// 턴 상태가 전부이고, 나머지는 화면을 같은 모습으로 되살리기 위한 값이다. 규칙: docs/spec/game/session.md
    /// </summary>
    public class SaveGameData
    {
        /// <summary>SaveSystem.Version과 다르면 읽지 않는다(옛 저장은 버린다).</summary>
        public int Version;
        public GridWorld Grid;
        public EntityWorld World;
        public EconomyWorld Economy;
        public TurnState Turn;
        public Dictionary<Team, int> TribeByTeam = new Dictionary<Team, int>();
        public Dictionary<Team, int> GodByTeam = new Dictionary<Team, int>();
        public Vector3 CameraFocus;
        public float CameraSize;
        /// <summary>시작 화면 "이어하기" 아래 요약 줄용(플레이어 종족 이름은 언어마다 다르므로 번호만).</summary>
        public int PlayerTribe = -1;
    }
}
