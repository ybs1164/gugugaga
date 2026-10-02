using System;
using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 유닛에 국한되지 않는 범용 엔티티-컴포넌트 저장소.
    /// 엔티티는 그 자체로는 아무 데이터도 갖지 않는 정수 id일 뿐이고, 실제 값(컴포넌트)은
    /// 컴포넌트 타입별로 나뉜 리스트에 저장된다 — 같은 id가 모든 컴포넌트 리스트에서 같은 인덱스를
    /// 가리키는 방식으로 하나의 엔티티를 구성한다.
    /// 이 클래스는 "유닛"이라는 개념을 전혀 모르고 값만 가진다. Set&lt;T&gt;(id, value) / Get&lt;T&gt;(id) 같은 접근은
    /// Systems/EntityQueries(확장 메서드), 컴포넌트가 무엇을 의미하는지 해석하는 일은 UnitQueries 등 Systems의 몫이다.
    /// </summary>
    public class EntityWorld
    {
        /// <summary>지금까지 만들어진 엔티티 수(= 다음 엔티티 id). 유닛이 죽어도 엔티티는 제거되지 않으므로 계속 유지된다.</summary>
        public int EntityCount;

        /// <summary>컴포넌트 타입 -> List&lt;T&gt; (인덱스 = 엔티티 id).</summary>
        public readonly Dictionary<Type, object> Pools = new Dictionary<Type, object>();
    }
}
