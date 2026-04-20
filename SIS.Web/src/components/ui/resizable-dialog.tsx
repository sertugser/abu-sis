import * as React from 'react';
import { useCallback, useRef, useState } from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { Maximize2, Minimize2, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { DialogOverlay, DialogPortal } from './dialog';

type ResizeDirection =
  | 'top'
  | 'bottom'
  | 'left'
  | 'right'
  | 'top-left'
  | 'top-right'
  | 'bottom-left'
  | 'bottom-right';

interface ResizableDialogContentProps {
  children: React.ReactNode;
  className?: string;
  defaultWidth?: number;
  defaultHeight?: number;
  minWidth?: number;
  minHeight?: number;
  showFullscreenToggle?: boolean;
}

const ResizableDialogContent = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Content>,
  ResizableDialogContentProps & Omit<React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content>, 'style'>
>(
  (
    {
      children,
      className,
      defaultWidth = 672,
      defaultHeight,
      minWidth = 400,
      minHeight = 300,
      showFullscreenToggle = true,
      ...props
    },
    ref,
  ) => {
    const [isFullscreen, setIsFullscreen] = useState(false);
    const [dimensions, setDimensions] = useState<{
      width: number;
      height: number;
      top: number;
      left: number;
    } | null>(null);
    const contentRef = useRef<HTMLDivElement | null>(null);
    const isResizingRef = useRef(false);
    const isDraggingRef = useRef(false);

    // ── Drag to move (direct DOM for performance) ──
    const startDrag = useCallback(
      (e: React.MouseEvent) => {
        if ((e.target as HTMLElement).closest('button, input, select, textarea, a')) return;
        e.preventDefault();

        const el = contentRef.current;
        if (!el) return;
        const rect = el.getBoundingClientRect();
        // Always use getBoundingClientRect — this gives the ACTUAL rendered position
        // regardless of CSS transform. Lock it immediately to prevent jump.
        const actualTop = rect.top;
        const actualLeft = rect.left;
        const actualWidth = rect.width;
        const actualHeight = rect.height;

        // If dimensions not yet set, initialize them NOW and fix position
        if (!dimensions) {
          el.style.top = `${actualTop}px`;
          el.style.left = `${actualLeft}px`;
          el.style.width = `${actualWidth}px`;
          el.style.height = `${actualHeight}px`;
          el.style.transform = 'none';
          el.style.maxWidth = 'none';
          el.style.maxHeight = 'none';
          setDimensions({ width: actualWidth, height: actualHeight, top: actualTop, left: actualLeft });
        }

        const startX = e.clientX;
        const startY = e.clientY;
        const startTop = actualTop;
        const startLeft = actualLeft;
        let rafId = 0;
        let latestTop = startTop;
        let latestLeft = startLeft;

        isDraggingRef.current = true;
        el.style.transition = 'none';

        function onMouseMove(ev: MouseEvent) {
          const w = el!.offsetWidth || actualWidth;
          const h = el!.offsetHeight || actualHeight;
          latestTop = Math.min(window.innerHeight - h, Math.max(0, startTop + (ev.clientY - startY)));
          latestLeft = Math.min(window.innerWidth - w, Math.max(0, startLeft + (ev.clientX - startX)));
          cancelAnimationFrame(rafId);
          rafId = requestAnimationFrame(() => {
            el!.style.top = `${latestTop}px`;
            el!.style.left = `${latestLeft}px`;
            el!.style.transform = 'none';
          });
        }

        function onMouseUp() {
          isDraggingRef.current = false;
          cancelAnimationFrame(rafId);
          setDimensions(prev => ({
            width: prev?.width ?? actualWidth,
            height: prev?.height ?? actualHeight,
            top: latestTop,
            left: latestLeft,
          }));
          el!.style.transition = '';
          document.removeEventListener('mousemove', onMouseMove);
          document.removeEventListener('mouseup', onMouseUp);
        }

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
      },
      [dimensions],
    );

    // Reset dimensions when dialog opens/closes
    const handleOpenAutoFocus = useCallback(
      (e: Event) => {
        // Reset to default centered state
        setDimensions(null);
        setIsFullscreen(false);
        props.onOpenAutoFocus?.(e);
      },
      [props],
    );

    const startResize = useCallback(
      (e: React.MouseEvent, direction: ResizeDirection) => {
        e.preventDefault();
        e.stopPropagation();

        if (!contentRef.current) return;
        const el = contentRef.current;
        const rect = el.getBoundingClientRect();

        // Lock position immediately if not yet set
        if (!dimensions) {
          el.style.top = `${rect.top}px`;
          el.style.left = `${rect.left}px`;
          el.style.width = `${rect.width}px`;
          el.style.height = `${rect.height}px`;
          el.style.transform = 'none';
          el.style.maxWidth = 'none';
          el.style.maxHeight = 'none';
          setDimensions({ width: rect.width, height: rect.height, top: rect.top, left: rect.left });
        }

        const startDims = { width: rect.width, height: rect.height, top: rect.top, left: rect.left };

        const startX = e.clientX;
        const startY = e.clientY;
        const startWidth = startDims.width;
        const startHeight = startDims.height;
        const startTop = startDims.top;
        const startLeft = startDims.left;

        const maxWidth = window.innerWidth - 16;
        const maxHeight = window.innerHeight - 16;

        isResizingRef.current = true;
        let rafId = 0;
        let latestW = startWidth;
        let latestH = startHeight;
        let latestT = startTop;
        let latestL = startLeft;
        el.style.transition = 'none';

        function clamp(val: number, min: number, max: number) {
          return Math.min(Math.max(val, min), max);
        }

        function onMouseMove(ev: MouseEvent) {
          const dx = ev.clientX - startX;
          const dy = ev.clientY - startY;

          latestW = startWidth; latestH = startHeight; latestT = startTop; latestL = startLeft;

          if (direction.includes('right')) latestW = clamp(startWidth + dx, minWidth, maxWidth);
          if (direction.includes('left')) { latestW = clamp(startWidth - dx, minWidth, maxWidth); latestL = startLeft + (startWidth - latestW); }
          if (direction.includes('bottom')) latestH = clamp(startHeight + dy, minHeight, maxHeight);
          if (direction === 'top' || direction === 'top-left' || direction === 'top-right') { latestH = clamp(startHeight - dy, minHeight, maxHeight); latestT = startTop + (startHeight - latestH); }

          cancelAnimationFrame(rafId);
          rafId = requestAnimationFrame(() => {
            el.style.width = `${latestW}px`;
            el.style.height = `${latestH}px`;
            el.style.top = `${latestT}px`;
            el.style.left = `${latestL}px`;
            el.style.transform = 'none';
            el.style.maxWidth = 'none';
            el.style.maxHeight = 'none';
          });
        }

        function onMouseUp() {
          isResizingRef.current = false;
          cancelAnimationFrame(rafId);
          setDimensions({ width: latestW, height: latestH, top: latestT, left: latestL });
          el.style.transition = '';
          document.removeEventListener('mousemove', onMouseMove);
          document.removeEventListener('mouseup', onMouseUp);
        }

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
      },
      [dimensions, minWidth, minHeight],
    );

    // Toggle fullscreen
    const toggleFullscreen = useCallback(() => {
      if (!isFullscreen) {
        // Before going fullscreen, capture current dimensions for restore
        if (!dimensions && contentRef.current) {
          const rect = contentRef.current.getBoundingClientRect();
          setDimensions({
            width: rect.width,
            height: rect.height,
            top: rect.top,
            left: rect.left,
          });
        }
      }
      setIsFullscreen((f) => !f);
    }, [isFullscreen, dimensions]);

    // Compute style
    const computedStyle: React.CSSProperties = isFullscreen
      ? {
          position: 'fixed',
          top: '2.5vh',
          left: '2.5vw',
          width: '95vw',
          height: '95vh',
          transform: 'none',
          maxWidth: '95vw',
          maxHeight: '95vh',
          transition: 'all 200ms ease',
        }
      : dimensions
        ? {
            position: 'fixed',
            top: dimensions.top,
            left: dimensions.left,
            width: dimensions.width,
            height: dimensions.height,
            transform: 'none',
            maxWidth: 'none',
            maxHeight: 'none',
            transition: undefined,
          }
        : {
            width: defaultWidth,
            height: defaultHeight,
            maxHeight: defaultHeight ?? '85vh',
          };

    // Combine refs
    const setRefs = useCallback(
      (node: HTMLDivElement | null) => {
        contentRef.current = node;
        if (typeof ref === 'function') ref(node);
        else if (ref) (ref as React.MutableRefObject<HTMLDivElement | null>).current = node;
      },
      [ref],
    );

    return (
      <DialogPortal>
        <DialogOverlay />
        <DialogPrimitive.Content
          ref={setRefs}
          onOpenAutoFocus={handleOpenAutoFocus}
          className={cn(
            'fixed z-50 border bg-card p-6 shadow-xl rounded-xl overflow-y-auto',
            'duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out',
            'data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
            'data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95',
            // Default centering (only when no custom dimensions)
            !dimensions && !isFullscreen && 'left-[50%] top-[50%] translate-x-[-50%] translate-y-[-50%]',
            className,
          )}
          style={computedStyle}
          {...props}
        >
          {/* Drag handle — invisible bar at the top for moving, stops before action buttons */}
          <div
            className="absolute top-0 left-0 right-[50%] h-10 cursor-move z-[5]"
            onMouseDown={startDrag}
          />

          {children}

          {/* Close button */}
          <DialogPrimitive.Close className="absolute right-4 top-4 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:pointer-events-none data-[state=open]:bg-accent data-[state=open]:text-muted-foreground">
            <X className="h-4 w-4" />
            <span className="sr-only">Kapat</span>
          </DialogPrimitive.Close>

          {/* Fullscreen toggle */}
          {showFullscreenToggle && (
            <button
              type="button"
              onClick={toggleFullscreen}
              className="absolute right-11 top-4 rounded-sm opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 h-4 w-4 inline-flex items-center justify-center"
              title={isFullscreen ? 'Küçült' : 'Tam Ekran'}
            >
              {isFullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
            </button>
          )}

          {/* ── Resize handles ── */}
          {!isFullscreen && (
            <>
              {/* Edges */}
              <div
                className="absolute top-0 left-2 right-2 h-1.5 cursor-n-resize"
                onMouseDown={(e) => startResize(e, 'top')}
              />
              <div
                className="absolute bottom-0 left-2 right-2 h-1.5 cursor-s-resize"
                onMouseDown={(e) => startResize(e, 'bottom')}
              />
              <div
                className="absolute left-0 top-2 bottom-2 w-1.5 cursor-w-resize"
                onMouseDown={(e) => startResize(e, 'left')}
              />
              <div
                className="absolute right-0 top-2 bottom-2 w-1.5 cursor-e-resize"
                onMouseDown={(e) => startResize(e, 'right')}
              />
              {/* Corners */}
              <div
                className="absolute top-0 left-0 w-3 h-3 cursor-nw-resize z-10"
                onMouseDown={(e) => startResize(e, 'top-left')}
              />
              <div
                className="absolute top-0 right-0 w-3 h-3 cursor-ne-resize z-10"
                onMouseDown={(e) => startResize(e, 'top-right')}
              />
              <div
                className="absolute bottom-0 left-0 w-3 h-3 cursor-sw-resize z-10"
                onMouseDown={(e) => startResize(e, 'bottom-left')}
              />
              <div
                className="absolute bottom-0 right-0 w-3 h-3 cursor-se-resize z-10"
                onMouseDown={(e) => startResize(e, 'bottom-right')}
              />
            </>
          )}
        </DialogPrimitive.Content>
      </DialogPortal>
    );
  },
);

ResizableDialogContent.displayName = 'ResizableDialogContent';

export default ResizableDialogContent;
