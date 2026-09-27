using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모델 파츠 표 — Assets/Resources/Models/의 CSV(건물/지형/유닛 소품)를 GameDataLoader.LoadAll이 파싱해 채운다.
    /// 모델 Id는 "종류.이름"(예: "Building.Farm", "Terrain.Forest", "Unit.cavalry"). 색은 팔레트 이름(ModelPalette.csv) 또는
    /// #RRGGBB. 1차 모델링 비교분석(docs/ModelingPlan.md) 결과 "파츠 CSV + 저폴리 절차 메시"를 택했다. 순수 데이터.
    /// </summary>
    public static class ModelDefinition
    {
        /// <summary>Resources 폴더 안 모델 CSV 경로들(확장자 제외). 같은 모델 Id가 여러 파일에 있으면 행이 합쳐진다.</summary>
        public static readonly string[] CsvResourcePaths = { "Models/BuildingModels", "Models/TerrainModels", "Models/UnitModels" };

        public const string PaletteResourcePath = "Models/ModelPalette";

        /// <summary>팔레트 이름 -&gt; 색(ModelPalette.csv).</summary>
        public static Dictionary<string, Color> Palette = new Dictionary<string, Color>();

        /// <summary>모델 Id -&gt; 조각들(CSV 행 순서).</summary>
        public static Dictionary<string, ModelPartInfo[]> Models = new Dictionary<string, ModelPartInfo[]>();

        /// <summary>표를 다시 읽을 때마다 1씩 오른다 — 조립한 메시를 캐시하는 쪽(ModelBuilder)이 옛 메시를 버리는 기준.</summary>
        public static int Version;
    }
}
