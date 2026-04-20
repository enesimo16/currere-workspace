'use client';

import { useRef, useState, useCallback, ReactNode } from 'react';

interface ResizablePanelsProps {
  leftPanel: ReactNode;
  rightPanel: ReactNode;
  defaultLeftPercent?: number;
  minLeftPercent?: number;
  maxLeftPercent?: number;
}

export default function ResizablePanels({
  leftPanel,
  rightPanel,
  defaultLeftPercent = 62,
  minLeftPercent = 25,
  maxLeftPercent = 80,
}: ResizablePanelsProps) {
  const [leftPercent, setLeftPercent] = useState(defaultLeftPercent);
  const [isDragging, setIsDragging] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    const startX = e.clientX;
    const startPercent = leftPercent;
    setIsDragging(true);

    const onMouseMove = (moveEvent: MouseEvent) => {
      if (!containerRef.current) return;
      const containerWidth = containerRef.current.getBoundingClientRect().width;
      const delta = moveEvent.clientX - startX;
      const deltaPercent = (delta / containerWidth) * 100;
      const newPercent = Math.min(maxLeftPercent, Math.max(minLeftPercent, startPercent + deltaPercent));
      setLeftPercent(newPercent);
    };

    const onMouseUp = () => {
      setIsDragging(false);
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }, [leftPercent, minLeftPercent, maxLeftPercent]);

  return (
    <div
      ref={containerRef}
      className="flex-1 flex overflow-hidden bg-[#0d0d0d]"
      style={{ userSelect: isDragging ? 'none' : undefined }}
    >
      {/* Left Panel */}
      <div className="flex flex-col overflow-hidden" style={{ width: `${leftPercent}%` }}>
        {leftPanel}
      </div>

      {/* Resize Handle — 2px divider only */}
      <div
        onMouseDown={handleMouseDown}
        className={`w-[2px] shrink-0 cursor-col-resize group transition-colors duration-150 ${
          isDragging ? 'bg-emerald-500' : 'bg-[#2a2a2a] hover:bg-emerald-500/60'
        }`}
      />

      {/* Right Panel */}
      <div className="flex flex-col overflow-hidden" style={{ width: `${100 - leftPercent}%` }}>
        {rightPanel}
      </div>
    </div>
  );
}
