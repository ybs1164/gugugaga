using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 절차 생성용 순수 노이즈 함수 하나만 담는 정적 클래스. 자체 상태를 갖지 않는다(규칙 3) — 같은
    /// (x, y, frequency, octaves, seedOffset)이면 항상 같은 값을 돌려준다(결정론적, 시드 재현 가능).
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>옥타브를 누적한 프랙탈 Perlin 노이즈를 0~1 범위로 정규화해 돌려준다. seedOffset은
        /// 같은 좌표라도 서로 다른 노이즈장을 얻기 위한 좌표 이동값이다(바이옴별, 또는 같은 바이옴 안
        /// 타일 엔트리별로 다른 값을 넘겨 서로 독립된 패턴을 만든다 — TerrainGenerationSystem 참고).</summary>
        public static float Sample(int x, int y, float frequency, int octaves, int seedOffset)
        {
            octaves = Mathf.Max(1, octaves);
            float nx = x * frequency + seedOffset;
            float ny = y * frequency + seedOffset;

            float value = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float freq = 1f;

            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(nx * freq, ny * freq) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                freq *= 2f;
            }

            return amplitudeSum > 0f ? Mathf.Clamp01(value / amplitudeSum) : 0f;
        }
    }
}
