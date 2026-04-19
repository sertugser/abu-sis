using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedEvaluationQuestionsAsync(AppDbContext context)
    {
        if (await context.EvaluationQuestions.AnyAsync())
            return;

        var questions = new List<EvaluationQuestion>
        {
            // Ders Kategorisi
            new() { Text = "Dersin icerigi, ogrenme hedeflerine uygundu", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Course, SortOrder = 1, IsActive = true },
            new() { Text = "Ders materyalleri (kitap, sunum, kaynak) yeterli ve gunceldi", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Course, SortOrder = 2, IsActive = true },
            new() { Text = "Ders is yuku (odev, proje, sinav) dengeliydi", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Course, SortOrder = 3, IsActive = true },
            new() { Text = "Degerlendirme yontemleri (sinav, odev) adil ve seffafti", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Course, SortOrder = 4, IsActive = true },
            new() { Text = "Ders hakkinda eklemek istediginiz gorusleriniz", QuestionType = EvaluationQuestionType.Text, Category = EvaluationQuestionCategory.Course, SortOrder = 5, IsActive = true },

            // Ogretim Uyesi Kategorisi
            new() { Text = "Ogretim uyesi konulari anlasilir ve etkili bicimde isledi", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Instructor, SortOrder = 1, IsActive = true },
            new() { Text = "Ogretim uyesi derslere hazirlikli ve duzenli geldi", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Instructor, SortOrder = 2, IsActive = true },
            new() { Text = "Ogretim uyesi soru ve geri bildirimlere acikti", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Instructor, SortOrder = 3, IsActive = true },
            new() { Text = "Ogretim uyesi ogrencilere saygi ve esitlikle yaklasti", QuestionType = EvaluationQuestionType.Rating, Category = EvaluationQuestionCategory.Instructor, SortOrder = 4, IsActive = true },
            new() { Text = "Ogretim uyesi hakkinda eklemek istediginiz gorusleriniz", QuestionType = EvaluationQuestionType.Text, Category = EvaluationQuestionCategory.Instructor, SortOrder = 5, IsActive = true },
        };

        context.EvaluationQuestions.AddRange(questions);
        await context.SaveChangesAsync();
    }
}
