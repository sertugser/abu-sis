import { Badge } from '@/components/ui/badge';
import { STATUS_LABELS, STATUS_VARIANT } from '../../lib/constants';

interface Props {
  status: string;
}

export default function RegistrationStatusBadge({ status }: Props) {
  return (
    <Badge variant={STATUS_VARIANT[status] || 'secondary'}>
      {STATUS_LABELS[status] || status}
    </Badge>
  );
}
