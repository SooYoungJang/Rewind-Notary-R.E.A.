namespace RewindNotary
{
    public sealed class StageEntry
    {
        public readonly string Id, Name, UnlockFlag;
        public readonly bool Implemented;
        public StageEntry(string id, string name, string flag, bool implemented = false)
        { Id = id; Name = name; UnlockFlag = flag; Implemented = implemented; }
        public bool Unlocked(CoreRecord record) => string.IsNullOrEmpty(UnlockFlag) || (record != null && record.storyFlags.Contains(UnlockFlag));
        public string Status(CoreRecord record) => !Unlocked(record) ? "잠금 · 준비 중" : Implemented ? "플레이 가능" : "준비 중";
    }
    public static class StageCatalog
    {
        // Flags are narrative hooks, never inferred from mastery counts or prototype boss victory.
        public static readonly StageEntry[] All = {
            new StageEntry("hub", "거점 · 망각 다락방", "hub_open"),
            new StageEntry("velvet", "제1구역 · 벨벳 재단거리 / 거대 직조 공방", "districts_open"),
            new StageEntry("clock", "제2구역 · 황혼의 태엽 시계탑 / 역류 회랑", "districts_open"),
            new StageEntry("opera", "제3구역 · 유리 오페라 극장 / 스포트라이트 지붕", "districts_open"),
            new StageEntry("bell", "제4구역 · 청동 대종루 / 공명 소리굽쇠 지하도", "districts_open"),
            new StageEntry("gallery", "제5구역 · 하얀 캔버스 화랑 / 색채의 온실", "districts_open"),
            new StageEntry("janhyang", "잔향전", "janhyang_open"),
            new StageEntry("cassel", "카셀전", "cassel_open"),
            new StageEntry("prototype", "기존 테스트 스테이지 · 이동 / 검술 / 잔향 대련", "", true),
            new StageEntry("practice_julius", "기술 대련 · 율리우스 잔향 (진자 보상)", "", true),
            new StageEntry("practice_valeria", "기술 대련 · 발레리아 잔향 (원무 보상)", "", true),
            new StageEntry("practice_gabriel", "기술 대련 · 가브리엘 잔향 (단조 보상)", "", true),
            new StageEntry("practice_irina", "기술 대련 · 이리나 잔향 (화폭 보상)", "", true),
            new StageEntry("skill_lab", "전체 기술 연습장 · 즉시 지급 / 검증 저장", "", true)
        };
        public static StageEntry Find(string id) => System.Array.Find(All, entry => entry.Id == id);
    }
}
