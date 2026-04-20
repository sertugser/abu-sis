import { useState, useRef, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Input } from '@/components/ui/input';
import { Users } from 'lucide-react';
import apiClient from '../../api/axios';
import { useDebounce } from '../../hooks/useDebounce';

interface UserOption {
  id: number;
  fullName: string;
  email: string;
}

interface UserSearchResult {
  items: UserOption[];
  totalCount: number;
}

interface UserSearchComboboxProps {
  value: string;
  onChange: (userId: string) => void;
}

export function UserSearchCombobox({ value, onChange }: UserSearchComboboxProps) {
  const [searchText, setSearchText] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const debouncedSearch = useDebounce(searchText, 300);
  const containerRef = useRef<HTMLDivElement>(null);

  const { data } = useQuery({
    queryKey: ['admin', 'users', 'search', debouncedSearch],
    queryFn: async () => {
      const { data } = await apiClient.get<UserSearchResult>('/admin/users', {
        params: { search: debouncedSearch, pageSize: 10 },
      });
      return data;
    },
    enabled: debouncedSearch.length >= 2,
    staleTime: 30_000,
  });

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setShowDropdown(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  function handleSelect(user: UserOption) {
    onChange(String(user.id));
    setSearchText(user.fullName);
    setShowDropdown(false);
  }

  function handleClear() {
    onChange('');
    setSearchText('');
    setShowDropdown(false);
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Users className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
        <Input
          className="h-9 w-48 pl-7 pr-7"
          placeholder="Kullanıcı ara..."
          value={value ? (searchText || `#${value}`) : searchText}
          onChange={e => {
            setSearchText(e.target.value);
            setShowDropdown(true);
            if (!e.target.value) handleClear();
          }}
          onFocus={() => { if (searchText.length >= 2) setShowDropdown(true); }}
        />
        {value && (
          <button
            type="button"
            className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground text-xs"
            onClick={handleClear}
          >
            ✕
          </button>
        )}
      </div>

      {showDropdown && data?.items && data.items.length > 0 && (
        <div className="absolute z-50 mt-1 w-64 rounded-md border bg-popover shadow-lg overflow-hidden">
          {data.items.map(user => (
            <button
              key={user.id}
              type="button"
              className="w-full flex items-center gap-2 px-3 py-2 text-left hover:bg-muted/50 transition-colors"
              onClick={() => handleSelect(user)}
            >
              <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary text-[9px] font-bold">
                {user.fullName.split(' ').map(p => p[0]).join('').substring(0, 2).toUpperCase()}
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium truncate">{user.fullName}</p>
                <p className="text-[11px] text-muted-foreground truncate">{user.email} · ID: {user.id}</p>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
