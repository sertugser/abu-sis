using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Plan 9'da V1 engine (MRV+SA+LNS+Construction+FailureAnalyzer) silindi (~5500 satır).
    /// Bu V1 algoritma config key'leri DB'de + Algoritma Parametreleri sekmesinde kaldı ama
    /// hiçbir kod referans vermiyor — atıl. Admin değiştirebiliyordu ama solver okumuyordu.
    /// 2026-05-10 dependency check: kod tarafında 0 referans (sadece SeedData + Controller
    /// NumericLimits dict). Aşağıdaki 19 key güvenle siliniyor.
    ///
    /// NOT: "Scheduling.MaxBacktrackingDepth" SchedulingService.BatchAuto.cs:169'da hâlâ
    /// kullanılıyor (eski /scheduling/batch-auto-schedule endpoint'i). Bu key DOKUNULMADI.
    /// </summary>
    public partial class RemoveAtilV1AlgorithmKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""SystemConfigs""
                WHERE ""Key"" IN (
                    'Schedule.SA.MaxIterations',
                    'Schedule.SA.InitialTemperature',
                    'Schedule.SA.TimeoutSeconds',
                    'Schedule.SA.StagnationLimit',
                    'Schedule.SA.BacktrackLimit',
                    'Schedule.SA.RelocateWeight',
                    'Schedule.SA.SwapWeight',
                    'Schedule.SA.ChainWeight',
                    'Schedule.LNS.MaxIterations',
                    'Schedule.LNS.TimeoutSeconds',
                    'Schedule.LNS.DestroySize',
                    'Schedule.MRV.ClassroomWeight',
                    'Schedule.MRV.SlotWeight',
                    'Schedule.MRV.LabWeight',
                    'Schedule.MRV.CapacityWeight',
                    'Schedule.Construction.SoftLookaheadCount',
                    'Schedule.Candidate.MaxCombinations',
                    'Schedule.Candidate.MaxWindowsPerDay',
                    'Schedule.Candidate.MaxClassroomsPerType'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: Plan 9 V1 silinmeden önceki default değerlerle yeniden ekle.
            // Bu silinen key'lerin solver tarafında hiçbir etkisi yok — geri alma sadece
            // schema rollback için sembolik (admin Algoritma Parametreleri panel'inde
            // tekrar görünür hale gelir).
            migrationBuilder.Sql(@"
                INSERT INTO ""SystemConfigs"" (""Key"", ""Value"", ""Description"", ""Category"", ""DataType"")
                VALUES
                    ('Schedule.SA.MaxIterations', '10000', '(V1 atıl) Motor en fazla kaç iyileştirme denemesi yapsın.', 'SA', 'int'),
                    ('Schedule.SA.InitialTemperature', '100', '(V1 atıl) Motor başlangıç sıcaklığı.', 'SA', 'int'),
                    ('Schedule.SA.TimeoutSeconds', '90', '(V1 atıl) Motor max süre (saniye).', 'SA', 'int'),
                    ('Schedule.SA.StagnationLimit', '2000', '(V1 atıl) Stagnation limit.', 'SA', 'int'),
                    ('Schedule.SA.BacktrackLimit', '50', '(V1 atıl) Construction backtrack limit.', 'SA', 'int'),
                    ('Schedule.SA.RelocateWeight', '50', '(V1 atıl) Relocate hamlesi ağırlığı.', 'SA', 'int'),
                    ('Schedule.SA.SwapWeight', '35', '(V1 atıl) Swap hamlesi ağırlığı.', 'SA', 'int'),
                    ('Schedule.SA.ChainWeight', '15', '(V1 atıl) Chain hamlesi ağırlığı.', 'SA', 'int'),
                    ('Schedule.LNS.MaxIterations', '100', '(V1 atıl) LNS yan motor max iterasyon.', 'LNS', 'int'),
                    ('Schedule.LNS.TimeoutSeconds', '30', '(V1 atıl) LNS yan motor süre.', 'LNS', 'int'),
                    ('Schedule.LNS.DestroySize', '8', '(V1 atıl) LNS destroy size.', 'LNS', 'int'),
                    ('Schedule.MRV.ClassroomWeight', '3', '(V1 atıl) MRV derslik ağırlığı.', 'MRV', 'int'),
                    ('Schedule.MRV.SlotWeight', '2', '(V1 atıl) MRV slot ağırlığı.', 'MRV', 'int'),
                    ('Schedule.MRV.LabWeight', '2', '(V1 atıl) MRV lab ağırlığı.', 'MRV', 'int'),
                    ('Schedule.MRV.CapacityWeight', '1', '(V1 atıl) MRV kapasite ağırlığı.', 'MRV', 'int'),
                    ('Schedule.Construction.SoftLookaheadCount', '10', '(V1 atıl) Construction soft lookahead sayısı.', 'Other', 'int'),
                    ('Schedule.Candidate.MaxCombinations', '500', '(V1 atıl) CandidateGenerator max kombinasyon.', 'Other', 'int'),
                    ('Schedule.Candidate.MaxWindowsPerDay', '10', '(V1 atıl) CandidateGenerator max window/day.', 'Other', 'int'),
                    ('Schedule.Candidate.MaxClassroomsPerType', '25', '(V1 atıl) CandidateGenerator max classroom/type.', 'Other', 'int');
            ");
        }
    }
}
