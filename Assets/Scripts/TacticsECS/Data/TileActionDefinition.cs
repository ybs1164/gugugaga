namespace TacticsECS
{
    /// <summary>
    /// 타일 행동(채집/벌목/화전/숲 조성/건물 파괴) 고정 표 — 폴리토피아 위키 Population 문서의 비용/인구 값.
    /// 불가사리 인양(항해)만 위키에서 유닛 행동(+별 8)이지만, 여기서는 영토 안 타일 행동으로 단순화했다.
    /// "Resource_Food"는 9차 재정비 이전 CSV의 옛 Id라 과일과 같이 취급한다.
    /// </summary>
    public static class TileActionDefinition
    {
        private static readonly string[] None = new string[0];

        public static readonly TileActionInfo[] All =
        {
            new TileActionInfo { Id = "HarvestFruit", Name = "과일 채집", Kind = TileActionKind.Harvest, UnlockKey = "Harvest.Fruit", Cost = 2,
                Terrain = TileClass.Field, RequiredStructures = new[] { "Resource_Fruit", "Resource_Food" }, Population = 1, Description = "인구 +1." },
            new TileActionInfo { Id = "HuntAnimal", Name = "사냥", Kind = TileActionKind.Harvest, UnlockKey = "Harvest.Animal", Cost = 2,
                Terrain = TileClass.Forest, RequiredStructures = new[] { "Resource_Animal" }, Population = 1, Description = "인구 +1." },
            new TileActionInfo { Id = "CatchFish", Name = "낚시", Kind = TileActionKind.Harvest, UnlockKey = "Harvest.Fish", Cost = 2,
                Terrain = TileClass.ShallowWater | TileClass.Ocean, RequiredStructures = new[] { "Resource_Fish" }, Population = 1, Description = "인구 +1." },
            new TileActionInfo { Id = "HarvestStarfish", Name = "불가사리 인양", Kind = TileActionKind.Harvest, UnlockKey = "Harvest.Starfish", Cost = 0,
                Terrain = TileClass.ShallowWater | TileClass.Ocean, RequiredStructures = new[] { "Starfish" }, GoldGain = 8, Description = "골드 +8." },
            new TileActionInfo { Id = "ClearForest", Name = "벌목", Kind = TileActionKind.ClearForest, UnlockKey = "Ability.ClearForest", Cost = 0,
                Terrain = TileClass.Forest, RequiredStructures = None, GoldGain = 1, Description = "숲을 평지로. 골드 +1." },
            new TileActionInfo { Id = "BurnForest", Name = "화전", Kind = TileActionKind.BurnForest, UnlockKey = "Ability.BurnForest", Cost = 5,
                Terrain = TileClass.Forest, RequiredStructures = None, Description = "숲을 작물이 있는 평지로." },
            new TileActionInfo { Id = "GrowForest", Name = "숲 조성", Kind = TileActionKind.GrowForest, UnlockKey = "Ability.GrowForest", Cost = 5,
                Terrain = TileClass.Field, RequiredStructures = None, Description = "평지를 숲으로." },
            new TileActionInfo { Id = "DestroyBuilding", Name = "건물 파괴", Kind = TileActionKind.Destroy, UnlockKey = "Ability.Destroy", Cost = 0,
                Terrain = TileClass.Field | TileClass.Forest | TileClass.Mountain | TileClass.ShallowWater | TileClass.Ocean,
                RequiredStructures = None, Description = "건물을 없애고 그 인구를 되돌린다." },
        };
    }
}
