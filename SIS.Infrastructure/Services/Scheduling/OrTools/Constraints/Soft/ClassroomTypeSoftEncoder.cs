using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// H4 — ClassroomType soft mode (Plan 10 Phase 3).
/// Hard <see cref="Hard.ClassroomTypeEncoder"/> tip-uyumsuz odaları <c>roomVar != classroom.Id</c>
/// ile yasaklar. Soft modda yasak kaldırılır; tip-uyumsuz bir oda KULLANILIRSA bileşen başına
/// <see cref="SoftThresholds.H4Weight"/> penalty eklenir (admin "tercihli": uyumsuz oda son çare
/// olarak kabul edilir). Hard encoder ile aynı izin (isAllowed / meets) mantığı; tek fark
/// <c>!= cid</c> yerine <c>== cid</c> reify edilip penalty'ye bağlanması.
///
/// Bir oda IntVar'ı tam bir classroom seçtiğinden, yasaklı oda kümesinden en çok biri "kullanılmış"
/// olur → bileşen başına en çok bir kez weight uygulanır.
/// </summary>
public sealed class ClassroomTypeSoftEncoder : IConstraintEncoder
{
    public string Code => "H4";
    public bool IsHard => false;

    public void Encode(EncoderContext ctx)
    {
        if (!ctx.ConstraintFlags.GetValueOrDefault(Code, true)) return;

        var category = ctx.Request.ConstraintCategories.GetValueOrDefault(Code, "Hard");
        if (!string.Equals(category, "Soft", StringComparison.OrdinalIgnoreCase))
            return;

        var weight = (long)ctx.Request.SoftThresholds.H4Weight;
        if (weight <= 0) return;

        foreach (var section in ctx.Request.Sections)
        {
            // Online section'ın oda IntVar'ı yok (ScheduleModelBuilder CreateRoomVariable atlar).
            if (section.TeachingMethod == TeachingMethod.Online) continue;
            EncodeLabPenalty(ctx, section, weight);
            EncodeTheoryPenalty(ctx, section, weight);
        }
    }

    private void EncodeLabPenalty(EncoderContext ctx, SectionInput section, long weight)
    {
        if (section.LabHours == 0) return;
        var labRooms = ctx.Variables.GetLabRooms(section.Id);

        foreach (var classroom in ctx.Request.Classrooms)
        {
            bool isAllowed;
            if (ctx.ClassroomTypeRequirementEnabled && section.RequiredLabClassroomType.HasValue)
                isAllowed = (int)classroom.Type == section.RequiredLabClassroomType.Value;
            else
                isAllowed = classroom.Type == ClassroomType.Lab || classroom.Type == ClassroomType.Computer;

            if (isAllowed) continue;

            foreach (var roomVar in labRooms)
                AddRoomPenalty(ctx, roomVar, classroom.Id, weight, $"h4soft_lab_s{section.Id}_c{classroom.Id}_r");
        }
    }

    private void EncodeTheoryPenalty(EncoderContext ctx, SectionInput section, long weight)
    {
        if (section.TheoryHours == 0) return;
        var lectureRoom = ctx.Variables.GetLectureRoom(section.Id);

        var amphiRequired = ctx.AmphiThresholdEnabled && section.Capacity >= ctx.AmphiThreshold;
        var typeRequired = ctx.ClassroomTypeRequirementEnabled && section.RequiredTheoryClassroomType.HasValue;
        var lectureOnly = !typeRequired;

        if (!amphiRequired && !typeRequired && !lectureOnly) return;

        foreach (var classroom in ctx.Request.Classrooms)
        {
            bool meetsAmphi = !amphiRequired || classroom.Type == ClassroomType.Amphitheater;
            bool meetsType = !typeRequired || (int)classroom.Type == section.RequiredTheoryClassroomType!.Value;
            bool meetsLectureOnly = !lectureOnly || !classroom.Type.IsLabCategory();

            if (meetsAmphi && meetsType && meetsLectureOnly) continue;

            AddRoomPenalty(ctx, lectureRoom, classroom.Id, weight, $"h4soft_thy_s{section.Id}_c{classroom.Id}");
        }
    }

    // roomVar == classroomId ise penalty: eqLit ⇔ (roomVar == classroomId), Penalty += weight × eqLit.
    private static void AddRoomPenalty(EncoderContext ctx, IntVar roomVar, int classroomId, long weight, string name)
    {
        var eqLit = ctx.Model.NewBoolVar(name);
        ctx.Model.Add(roomVar == classroomId).OnlyEnforceIf(eqLit);
        ctx.Model.Add(roomVar != classroomId).OnlyEnforceIf(eqLit.Not());
        ctx.Penalties.Add(LinearExpr.Term(eqLit, weight));
    }
}
