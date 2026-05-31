import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { DialogPortal, DialogOverlay } from '@/components/ui/dialog';

// Re-export the Radix root so callers can use a single import surface.
const DraggableDialog = DialogPrimitive.Root;
const DraggableDialogTrigger = DialogPrimitive.Trigger;
const DraggableDialogClose = DialogPrimitive.Close;

interface Offset {
  x: number;
  y: number;
}

interface DragContextValue {
  onTitleBarPointerDown: (event: React.PointerEvent<HTMLElement>) => void;
}

const DragContext = React.createContext<DragContextValue | null>(null);

// Keep at least this many pixels of the dialog on-screen so it can never be
// dragged fully out of reach.
const ON_SCREEN_MARGIN = 48;

interface DraggableDialogContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  /** Set to false to disable the on-screen clamping (defaults to true). */
  clampToViewport?: boolean;
}

const DraggableDialogContent = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Content>,
  DraggableDialogContentProps
>(({ className, children, clampToViewport = true, ...props }, ref) => {
  // Offset starts centered (0,0). The content remounts on each open (Radix
  // unmounts the portal when closed), so no explicit reset effect is needed.
  const [offset, setOffset] = React.useState<Offset>({ x: 0, y: 0 });
  const dragOrigin = React.useRef<{ pointerX: number; pointerY: number; baseX: number; baseY: number } | null>(null);

  const clamp = React.useCallback(
    (next: Offset): Offset => {
      if (!clampToViewport || typeof window === 'undefined') return next;
      const maxX = window.innerWidth / 2 - ON_SCREEN_MARGIN;
      const maxY = window.innerHeight / 2 - ON_SCREEN_MARGIN;
      return {
        x: Math.min(maxX, Math.max(-maxX, next.x)),
        y: Math.min(maxY, Math.max(-maxY, next.y)),
      };
    },
    [clampToViewport],
  );

  const onTitleBarPointerDown = React.useCallback(
    (event: React.PointerEvent<HTMLElement>) => {
      // Ignore drags that start on interactive elements (e.g. the close button).
      if ((event.target as HTMLElement).closest('[data-no-drag]')) return;
      event.preventDefault();
      dragOrigin.current = {
        pointerX: event.clientX,
        pointerY: event.clientY,
        baseX: offset.x,
        baseY: offset.y,
      };

      const handleMove = (moveEvent: PointerEvent) => {
        const origin = dragOrigin.current;
        if (!origin) return;
        setOffset(
          clamp({
            x: origin.baseX + (moveEvent.clientX - origin.pointerX),
            y: origin.baseY + (moveEvent.clientY - origin.pointerY),
          }),
        );
      };

      const handleUp = () => {
        dragOrigin.current = null;
        window.removeEventListener('pointermove', handleMove);
        window.removeEventListener('pointerup', handleUp);
        window.removeEventListener('pointercancel', handleUp);
      };

      window.addEventListener('pointermove', handleMove);
      window.addEventListener('pointerup', handleUp);
      window.addEventListener('pointercancel', handleUp);
    },
    [offset.x, offset.y, clamp],
  );

  const contextValue = React.useMemo<DragContextValue>(
    () => ({ onTitleBarPointerDown }),
    [onTitleBarPointerDown],
  );

  return (
    <DialogPortal>
      <DialogOverlay />
      <DialogPrimitive.Content
        ref={ref}
        // Baseline centering (left/top 50%) plus the live drag offset, both folded
        // into a single transform so motion stays on the compositor.
        style={{
          transform: `translate(calc(-50% + ${offset.x}px), calc(-50% + ${offset.y}px))`,
        }}
        // Suppress the Radix a11y warning about a missing DialogDescription.
        // Draggable dialogs embed their own title-bar description; callers that
        // want a description can override this via props spread below.
        aria-describedby={undefined}
        className={cn(
          'fixed left-[50%] top-[50%] z-50 w-full max-w-lg gap-0 overflow-hidden border bg-card shadow-xl duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 rounded-xl',
          className,
        )}
        {...props}
      >
        <DragContext.Provider value={contextValue}>{children}</DragContext.Provider>
      </DialogPrimitive.Content>
    </DialogPortal>
  );
});
DraggableDialogContent.displayName = 'DraggableDialogContent';

interface DraggableDialogTitleBarProps {
  children: React.ReactNode;
  className?: string;
}

function DraggableDialogTitleBar({ children, className }: DraggableDialogTitleBarProps) {
  const ctx = React.useContext(DragContext);
  return (
    <div
      onPointerDown={ctx?.onTitleBarPointerDown}
      className={cn(
        'flex cursor-grab touch-none select-none items-center justify-between gap-2 active:cursor-grabbing',
        className,
      )}
    >
      <DialogPrimitive.Title className="min-w-0 flex-1 truncate text-sm font-semibold leading-tight">
        {children}
      </DialogPrimitive.Title>
      <DialogPrimitive.Close
        data-no-drag
        className="inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-md opacity-80 transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-1"
      >
        <X className="h-4 w-4" />
        <span className="sr-only">Kapat</span>
      </DialogPrimitive.Close>
    </div>
  );
}

export {
  DraggableDialog,
  DraggableDialogTrigger,
  DraggableDialogClose,
  DraggableDialogContent,
  DraggableDialogTitleBar,
};
