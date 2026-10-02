using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// EntityWorld(값만 가진 데이터)의 엔티티 생성·컴포넌트 읽기/쓰기. 확장 메서드라 호출은 world.Get&lt;T&gt;(id) 형태 그대로다.
    /// </summary>
    public static class EntityQueries
    {
        public static int CreateEntity(this EntityWorld world) => world.EntityCount++;

        public static void Set<T>(this EntityWorld world, int entity, T value)
        {
            var pool = GetPool<T>(world);
            while (pool.Count <= entity) pool.Add(default);
            pool[entity] = value;
        }

        public static T Get<T>(this EntityWorld world, int entity) => GetPool<T>(world)[entity];

        /// <summary>그 엔티티에 T가 한 번도 Set되지 않았으면 default(T). 나중에 추가된 컴포넌트(예: Embarked)를 모르는
        /// 옛 생성 코드(검증 스크립트의 수동 엔티티 등)와 함께 쓸 때 Get 대신 쓴다.</summary>
        public static T GetOrDefault<T>(this EntityWorld world, int entity)
        {
            var pool = GetPool<T>(world);
            return entity < pool.Count ? pool[entity] : default;
        }

        private static List<T> GetPool<T>(EntityWorld world)
        {
            var type = typeof(T);
            if (!world.Pools.TryGetValue(type, out var pool))
            {
                pool = new List<T>();
                world.Pools[type] = pool;
            }
            return (List<T>)pool;
        }
    }
}
