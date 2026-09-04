using System;
using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 유닛에 국한되지 않는 범용 엔티티-컴포넌트 저장소.
    /// 엔티티는 그 자체로는 아무 데이터도 갖지 않는 정수 id일 뿐이고, 실제 값(컴포넌트)은
    /// 컴포넌트 타입별로 나뉜 리스트에 저장된다 — 같은 id가 모든 컴포넌트 리스트에서 같은 인덱스를
    /// 가리키는 방식으로 하나의 엔티티를 구성한다.
    /// 이 클래스는 "유닛"이라는 개념을 전혀 모른다: Set&lt;T&gt;(id, value) / Get&lt;T&gt;(id)만 있을 뿐이고,
    /// 새 컴포넌트 타입(Core/UnitComponents.cs 등)을 추가해도 이 클래스는 손댈 필요가 없다.
    /// 지금은 모든 엔티티가 "유닛"(Team/GridPosition/Hp/... 컴포넌트를 가짐)이지만, 나중에 유닛이 아닌
    /// 다른 종류의 엔티티(장애물/투사체/아이템 등)가 생기더라도 이 저장소를 그대로 재사용할 수 있다.
    /// 컴포넌트 타입이 무엇을 의미하는지 해석하는 일은 EntityWorld가 아니라 Systems(UnitQueries 등)의 몫이다.
    /// </summary>
    public class EntityWorld
    {
        private int _nextId;
        private readonly Dictionary<Type, object> _pools = new Dictionary<Type, object>();

        /// <summary>지금까지 만들어진 엔티티 수. 유닛이 죽어도 엔티티는 제거되지 않으므로 계속 유지된다.</summary>
        public int EntityCount => _nextId;

        public int CreateEntity() => _nextId++;

        public void Set<T>(int entity, T value)
        {
            var pool = GetPool<T>();
            while (pool.Count <= entity) pool.Add(default);
            pool[entity] = value;
        }

        public T Get<T>(int entity) => GetPool<T>()[entity];

        private List<T> GetPool<T>()
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new List<T>();
                _pools[type] = pool;
            }
            return (List<T>)pool;
        }
    }
}
