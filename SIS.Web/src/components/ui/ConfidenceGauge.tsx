import { cn } from '@/lib/utils';

interface ConfidenceGaugeProps {
  /** 0 – 100 arası güven yüzdesi */
  value: number;
  /** Gauge altındaki açıklama */
  label?: string;
  /** px cinsinden genişlik (varsayılan 160) */
  size?: number;
  className?: string;
}

export default function ConfidenceGauge({
  value,
  label,
  size = 160,
  className,
}: ConfidenceGaugeProps) {
  const clamped = Math.max(0, Math.min(100, value));

  // Yarım daire: 180° toplam, 0% → sol uç, 100% → sağ uç
  const angle = (clamped / 100) * 180;

  // Renk geçişi: kırmızı → sarı → yeşil
  const getColor = (v: number) => {
    if (v < 40) return '#ef4444'; // red‑500
    if (v < 70) return '#f59e0b'; // amber‑500
    return '#22c55e'; // green‑500
  };

  const color = getColor(clamped);
  const radius = size / 2 - 12;
  const cx = size / 2;
  const cy = size / 2;

  // Arka plan yayı (gri)
  const bgArc = describeArc(cx, cy, radius, 180, 360);
  // Dolu yay
  const fillArc = describeArc(cx, cy, radius, 180, 180 + angle);

  // İbre ucu koordinatları
  const needleAngle = 180 + angle;
  const needleRad = (needleAngle * Math.PI) / 180;
  const needleX = cx + (radius - 6) * Math.cos(needleRad);
  const needleY = cy + (radius - 6) * Math.sin(needleRad);

  return (
    <div className={cn('flex flex-col items-center', className)}>
      <svg width={size} height={size / 2 + 20} viewBox={`0 0 ${size} ${size / 2 + 20}`}>
        {/* Arka plan yayı */}
        <path d={bgArc} fill="none" stroke="#e5e7eb" strokeWidth={10} strokeLinecap="round" />
        {/* Dolu yay */}
        <path
          d={fillArc}
          fill="none"
          stroke={color}
          strokeWidth={10}
          strokeLinecap="round"
          style={{ transition: 'stroke 0.4s, d 0.4s' }}
        />
        {/* İbre */}
        <line
          x1={cx}
          y1={cy}
          x2={needleX}
          y2={needleY}
          stroke={color}
          strokeWidth={2.5}
          strokeLinecap="round"
          style={{ transition: 'x2 0.4s, y2 0.4s, stroke 0.4s' }}
        />
        <circle cx={cx} cy={cy} r={4} fill={color} style={{ transition: 'fill 0.4s' }} />

        {/* Yüzde değeri */}
        <text
          x={cx}
          y={cy - 8}
          textAnchor="middle"
          className="text-2xl font-bold"
          fill={color}
          style={{ fontSize: size * 0.17, transition: 'fill 0.4s' }}
        >
          %{clamped}
        </text>

        {/* Uç etiketleri */}
        <text x={12} y={cy + 14} className="text-[10px]" fill="#9ca3af">%0</text>
        <text x={size - 24} y={cy + 14} className="text-[10px]" fill="#9ca3af">%100</text>
      </svg>

      {label && (
        <span className="mt-1 text-xs font-medium text-muted-foreground">{label}</span>
      )}
    </div>
  );
}

/* ── SVG yay yardımcısı ── */
function polarToCartesian(cx: number, cy: number, r: number, angleDeg: number) {
  const rad = (angleDeg * Math.PI) / 180;
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function describeArc(cx: number, cy: number, r: number, startAngle: number, endAngle: number) {
  const start = polarToCartesian(cx, cy, r, endAngle);
  const end = polarToCartesian(cx, cy, r, startAngle);
  const largeArc = endAngle - startAngle <= 180 ? '0' : '1';
  return `M ${start.x} ${start.y} A ${r} ${r} 0 ${largeArc} 0 ${end.x} ${end.y}`;
}
