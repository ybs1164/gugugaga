using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// TerrainGenerationSystem과 StructureGenerationSystem이 공유하는 순수 절차 생성 헬퍼. 상태를 갖지
    /// 않는다(규칙 3) — 매번 인자로 받은 값만으로 계산한다. 두 시스템이 각자 따로 구현하면 똑같은 코드가
    /// 중복되는 셔플/거리 계산/가중치 랜덤 선택만 모아뒀다.
    /// </summary>
    public static class ProceduralGenerationUtil
    {
        public static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static int ChebyshevDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>맵 가장자리까지의 최소 거리(위/아래/왼쪽/오른쪽 중 가장 가까운 쪽).</summary>
        public static int DistanceToEdge(GridWorld grid, Vector2Int pos) =>
            Mathf.Min(Mathf.Min(pos.x, pos.y), Mathf.Min(grid.Width - 1 - pos.x, grid.Height - 1 - pos.y));

        /// <summary>anchors 중 pos에 가장 가까운 것의 인덱스(유클리드 거리 제곱 기준) — Voronoi 분할에 쓰인다.</summary>
        public static int NearestAnchorIndex(Vector2Int[] anchors, Vector2Int pos)
        {
            int nearest = 0;
            int nearestDistSq = int.MaxValue;
            for (int i = 0; i < anchors.Length; i++)
            {
                int dx = anchors[i].x - pos.x;
                int dy = anchors[i].y - pos.y;
                int distSq = dx * dx + dy * dy;
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = i; }
            }
            return nearest;
        }

        /// <summary>(값, 가중치) 목록에서 가중치 비례 랜덤으로 하나를 뽑는다. 가중치 합이 0 이하면
        /// 균등 랜덤으로 폴백한다.</summary>
        public static T WeightedPick<T>(List<(T Entry, float Weight)> candidates, Random rng)
        {
            float total = 0f;
            foreach (var c in candidates) total += c.Weight;
            if (total <= 0f) return candidates[rng.Next(candidates.Count)].Entry;

            float roll = (float)(rng.NextDouble() * total);
            float acc = 0f;
            foreach (var c in candidates)
            {
                acc += c.Weight;
                if (roll <= acc) return c.Entry;
            }
            return candidates[candidates.Count - 1].Entry;
        }
    }
}
