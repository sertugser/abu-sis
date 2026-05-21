import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Skeleton } from '@/components/ui/skeleton';
import { useInstructorSections } from '../hooks/useInstructorSections';

export default function GradeEntryRedirectPage() {
  const navigate = useNavigate();
  const { data, isLoading } = useInstructorSections();

  useEffect(() => {
    const firstSection = data?.sections?.[0];
    if (firstSection) {
      navigate(`/not-girisi/${firstSection.sectionId}`, { replace: true });
    }
  }, [data, navigate]);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-16 rounded-xl" />
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  return (
    <div className="py-16 text-center text-muted-foreground text-[15px]">
      Bu donem not girisi yapabileceginiz ders bulunmamaktadir.
    </div>
  );
}
