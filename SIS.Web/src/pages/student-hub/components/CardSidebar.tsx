import { NavLink, useParams } from 'react-router-dom';
import { CARD_NAV } from '../sidebar.config';

export default function CardSidebar() {
  const { studentId } = useParams();
  const base = `/ogrenci-rehberi/${studentId}`;
  return (
    <nav aria-label="Öğrenci kart gezintisi" className="w-60 shrink-0 border-r border-border bg-white">
      {CARD_NAV.map(group => (
        <div key={group.title} className="py-2">
          <p className="px-4 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.title}</p>
          {group.items.map(item =>
            item.enabled ? (
              <NavLink key={item.segment || 'index'} to={item.segment ? `${base}/${item.segment}` : base} end={item.segment === ''}
                className={({ isActive }) => `block px-4 py-2 text-sm transition-colors ${isActive ? 'bg-primary/10 text-primary font-medium' : 'text-foreground hover:bg-muted/40'}`}>
                {item.label}
              </NavLink>
            ) : (
              <span key={item.segment} className="block px-4 py-2 text-sm text-muted-foreground/50 cursor-not-allowed" title="Yakında">{item.label}</span>
            )
          )}
        </div>
      ))}
    </nav>
  );
}
