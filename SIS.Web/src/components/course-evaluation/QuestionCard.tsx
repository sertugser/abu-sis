import { Card, CardContent } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import StarRating from './StarRating';
import type { EvaluationQuestion } from '../../types/courseEvaluation';

interface QuestionCardProps {
  question: EvaluationQuestion;
  ratingValue: number | null;
  textValue: string;
  onRatingChange: (value: number) => void;
  onTextChange: (value: string) => void;
}

export default function QuestionCard({
  question,
  ratingValue,
  textValue,
  onRatingChange,
  onTextChange,
}: QuestionCardProps) {
  return (
    <Card className="border border-border/60">
      <CardContent className="p-4 space-y-3">
        <p className="text-sm font-medium text-foreground leading-relaxed">{question.text}</p>

        {question.questionType === 'Rating' ? (
          <div className="flex items-center gap-3">
            <StarRating value={ratingValue} onChange={onRatingChange} />
            <span className="text-xs text-muted-foreground">
              {ratingValue ? `${ratingValue}/5` : 'Seçim yapın'}
            </span>
          </div>
        ) : (
          <Textarea
            value={textValue}
            onChange={(e) => onTextChange(e.target.value)}
            placeholder="Görüşlerinizi yazın (opsiyonel)"
            rows={3}
            maxLength={2000}
          />
        )}
      </CardContent>
    </Card>
  );
}
