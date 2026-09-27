using UnityEngine;

namespace TacticsECS
{
    /// <summary>모델 조각의 기본 도형 — 전부 "바닥 중심이 원점, 크기 1"인 저폴리 단위 도형(View/LowPolyMeshes).</summary>
    public enum ModelShape
    {
        /// <summary>상자.</summary>
        Box,
        /// <summary>박공지붕(삼각기둥) — 용마루가 X축을 따라 간다.</summary>
        Gable,
        /// <summary>사각뿔.</summary>
        Pyramid,
        /// <summary>8각 원뿔.</summary>
        Cone,
        /// <summary>8각 원기둥.</summary>
        Cylinder,
        /// <summary>위가 좁은(윗면 반지름 60%) 8각 원기둥 — 탑/굴뚝.</summary>
        Tapered,
        /// <summary>저폴리 구(바닥이 y=0).</summary>
        Sphere,
        /// <summary>절두 사각뿔(윗면 60%) — 계단식 기단/대장간 몸체.</summary>
        Frustum,
    }

    /// <summary>
    /// 모델 하나를 이루는 조각 하나 — Assets/Resources/Models/*.csv의 한 행. 순수 데이터(CLAUDE.md 규칙 2):
    /// 조립은 View/ModelBuilder, 표는 Data/ModelDefinition. 좌표는 타일 로컬(타일 한 변 = 1, 타일 윗면 y = 0),
    /// Position은 도형의 "바닥 중심"이다.
    /// </summary>
    public struct ModelPartInfo
    {
        public string Model;
        public ModelShape Shape;
        public Vector3 Position;
        public Vector3 Size;
        public Vector3 Rotation;
        public Color Color;

        /// <summary>이 조각이 보이는 최소 레벨(신전 레벨, 가공 건물의 인접 기반 건물 수 등). 0이면 항상.</summary>
        public int MinLevel;

        /// <summary>이 조각이 보이는 최대 레벨(0 = 제한 없음) — 레벨이 오르면 바뀌는 조각(예: 신전 지붕 높이).</summary>
        public int MaxLevel;

        /// <summary>팀 색으로 칠하는 조각(깃발/유닛 옷 등) — Color 대신 호출자가 넘긴 팀 색을 쓴다.</summary>
        public bool TeamColor;
    }
}
