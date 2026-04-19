import { useMemo, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import QuestionCard from './QuestionCard';
import {
  useActiveEvaluationQuestions,
  useSubmitEvaluation,
} from '../../hooks/useCourseEvaluation';
import type { EvaluableCourse, EvaluationAnswerPayload } from '../../types/courseEvaluation';
import { AlertCircle, CheckCircle2 } from 'lucide-react';

interface EvaluationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  course: EvaluableCourse | null;
}

interface AnswerState {
  rating: number | null;
  text: string;
}

export default function EvaluationDialog({ open, onOpenChange, course }: EvaluationDialogProps) {
  const { data: questions, isLoading } = useActiveEvaluationQuestions(open);
  const submitMutation = useSubmitEvaluation();
  const [answers, setAnswers] = useState<Record<number, AnswerState>>({});
  const [error, setError] = useState<string | null>(null);

  const sortedQuestions = useMemo(() => {
    if (!questions) return [];
    return [...questions].sort((a, b) => {
      if (a.category !== b.category) return a.category === 'Course' ? -1 : 1;
      return a.sortOrder - b.sortOrder;
    });
  }, [questions]);

  const courseQuestions = sortedQuestions.filter((q) => q.category === 'Course');
  const instructorQuestions = sortedQuestions.filter((q) => q.category === 'Instructor');

  function setRating(questionId: number, rating: number) {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: { ...(prev[questionId] ?? { rating: null, text: '' }), rating },
    }));
  }

  function setText(questionId: number, text: string) {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: { ...(prev[questionId] ?? { rating: null, text: '' }), text },
    }));
  }

  async function handleSubmit() {
    if (!course) return;
    setError(null);

    const missingRatings = sortedQuestions
      .filter((q) => q.questionType === 'Rating')
      .filter((q) => answers[q.id]?.rating == null);

    if (missingRatings.length > 0) {
      setError(`Tüm puanlama sorularını yanıtlayın (${missingRatings.length} soru eksik).`);
      return;
    }

    const payload: EvaluationAnswerPayload[] = sortedQuestions.map((q) => ({
      questionId: q.id,
      ratingValue: q.questionType === 'Rating' ? (answers[q.id]?.rating ?? null) : null,
      textValue:
        q.questionType === 'Text'
          ? (answers[q.id]?.text?.trim() ? answers[q.id].text.trim() : null)
          : null,
    }));

    try {
      await submitMutation.mutateAsync({
        courseSectionId: course.courseSectionId,
        answers: payload,
      });
      setAnswers({});
      onOpenChange(false);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Değerlendirme gönderilemedi.';
      setError(message);
    }
  }

  function handleClose(next: boolean) {
    if (!next) {
      setAnswers({});
      setError(null);
    }
    onOpenChange(next);
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Ders Değerlendirme</DialogTitle>
          {course && (
            <DialogDescription>
              <span className="font-semibold text-foreground">{course.courseCode}</span> —{' '}
              {course.courseName} · {course.instructorName}
            </DialogDescription>
          )}
        </DialogHeader>

        <Alert className="bg-blue-50 border-blue-200">
          <CheckCircle2 className="h-4 w-4 text-blue-600" />
          <AlertDescription className="text-blue-900 text-xs">
            Yanıtlarınız anonim olarak saklanır. Kimliğiniz öğretim üyesi veya yönetimle paylaşılmaz.
          </AlertDescription>
        </Alert>

        {isLoading ? (
          <div className="space-y-3">
            {[1, 2, 3, 4, 5].map((i) => (
              <Skeleton key={i} className="h-24 rounded-lg" />
            ))}
          </div>
        ) : (
          <div className="space-y-5">
            {courseQuestions.length > 0 && (
              <div className="space-y-3">
                <Badge variant="secondary" className="text-xs">Ders Hakkında</Badge>
                {courseQuestions.map((q) => (
                  <QuestionCard
                    key={q.id}
                    question={q}
                    ratingValue={answers[q.id]?.rating ?? null}
                    textValue={answers[q.id]?.text ?? ''}
                    onRatingChange={(v) => setRating(q.id, v)}
                    onTextChange={(v) => setText(q.id, v)}
                  />
                ))}
              </div>
            )}

            {instructorQuestions.length > 0 && (
              <div className="space-y-3">
                <Badge variant="secondary" className="text-xs">Öğretim Üyesi Hakkında</Badge>
                {instructorQuestions.map((q) => (
                  <QuestionCard
                    key={q.id}
                    question={q}
                    ratingValue={answers[q.id]?.rating ?? null}
                    textValue={answers[q.id]?.text ?? ''}
                    onRatingChange={(v) => setRating(q.id, v)}
                    onTextChange={(v) => setText(q.id, v)}
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => handleClose(false)} disabled={submitMutation.isPending}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={submitMutation.isPending || isLoading}>
            {submitMutation.isPending ? 'Gönderiliyor...' : 'Gönder'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
