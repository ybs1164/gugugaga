namespace TacticsECS
{
    /// <summary>표 팩(Sandbox가 내보낸 zip, 휴대폰이 가져온 폴더) 하나의 요약(값만). 형식: docs/spec/csv/table-pack.md</summary>
    public struct TablePackInfo
    {
        public string Name;
        /// <summary>표 내용 전체의 해시 앞자리 — 내보낸 PC와 가져온 휴대폰에서 같은 값인지 눈으로 맞춰 본다.</summary>
        public string Id;
        public int FileCount;
        /// <summary>내보낸 빌드의 Application.version.</summary>
        public string BuildVersion;
    }
}
