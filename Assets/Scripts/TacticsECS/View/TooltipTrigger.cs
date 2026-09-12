using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TacticsECS
{
    /// <summary>버튼/배지에 올라간 마우스 진입/이탈만 밖(BattleHud)으로 전달하는 얇은 컴포넌트. 어떤
    /// 텍스트를 보여줄지, 실제로 어떻게 보여줄지는 전혀 모른다(로직 없는 View 보조 도구).
    /// BattleHud의 중첩 클래스가 아니라 독립 파일의 최상위 클래스다 — 한 .cs 파일에 클래스를 두 개
    /// 넣으면(중첩이든 그냥 나열이든) Unity가 두 번째 클래스의 스크립트 참조(m_Script)를 그 파일이
    /// 재컴파일된 바로 그 배치 실행 안에서는 안정적으로 해석하지 못해(저장 직후엔 멀쩡해 보여도, 다시
    /// 불러오면 m_Script가 fileID 0으로 끊겨 컴포넌트가 null이 되는 것을 UIPrefabSetup 실행으로 실측
    /// 확인), 이 프로젝트의 다른 모든 MonoBehaviour와 같이 파일 하나당 클래스 하나로 둔다.</summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text;
        public Action<string> OnEnter;
        public Action OnExit;
        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke(Text);
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
    }
}
