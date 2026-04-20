'use client';

import React, { useMemo, useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';

interface CsvTableProps {
  csvData: string;
  fileName: string;
}

type ColType = 'number' | 'date' | 'boolean' | 'string';

interface ParsedCSV {
  headers: string[];
  colTypes: ColType[];
  rows: string[][];
}

function detectColType(samples: string[]): ColType {
  const nonEmpty = samples.filter(s => s.trim() !== '');
  if (nonEmpty.length === 0) return 'string';
  if (nonEmpty.every(v => !isNaN(Number(v.replace(/,/g, ''))))) return 'number';
  if (nonEmpty.every(v => ['true', 'false', '1', '0', 'yes', 'no'].includes(v.toLowerCase()))) return 'boolean';
  const dateRegex = /^\d{1,4}[-/]\d{1,2}[-/]\d{1,4}$/;
  if (nonEmpty.every(v => dateRegex.test(v.trim()))) return 'date';
  return 'string';
}

function parseCSV(text: string): ParsedCSV {
  const lines = text.split('\n').filter(line => line.trim() !== '');
  if (lines.length === 0) return { headers: [], colTypes: [], rows: [] };
  const headers = lines[0].split(',').map(h => h.trim().replace(/^"|"$/g, ''));
  const rows = lines.slice(1).map(line => line.split(',').map(c => c.trim().replace(/^"|"$/g, '')));
  const sampleRows = rows.slice(0, 100);
  const colTypes: ColType[] = headers.map((_, i) => detectColType(sampleRows.map(r => r[i] ?? '')));
  return { headers, colTypes, rows };
}

function TypeIcon({ type }: { type: ColType }) {
  const map: Record<ColType, { label: string; cls: string }> = {
    number:  { label: '#',   cls: 'text-sky-400/70' },
    date:    { label: '~',   cls: 'text-amber-400/70' },
    boolean: { label: '√',   cls: 'text-emerald-400/70' },
    string:  { label: 'Abc', cls: 'text-zinc-500' },
  };
  const { label, cls } = map[type];
  return (
    <span className={`text-[9px] font-bold font-mono shrink-0 ${cls}`}>
      {label}
    </span>
  );
}

const ROW_HEIGHT = 32;

export default function CsvTable({ csvData, fileName }: CsvTableProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const { headers, colTypes, rows } = useMemo(() => parseCSV(csvData), [csvData]);

  const rowVirtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => containerRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 20,
  });

  const virtualItems = rowVirtualizer.getVirtualItems();
  const totalSize = rowVirtualizer.getTotalSize();

  const colAlign: Record<ColType, string> = {
    number:  'text-right',
    boolean: 'text-center',
    date:    'text-left',
    string:  'text-left',
  };

  const colFontStyle: Record<ColType, string> = {
    number:  'font-mono text-sky-300/90',
    boolean: 'font-mono text-emerald-400/80',
    date:    'text-amber-300/80',
    string:  'text-zinc-300',
  };

  return (
    <div className="flex-1 flex flex-col bg-[#0e0e0e] overflow-hidden min-w-0">
      {/* Top Info Bar */}
      <div className="h-9 border-b border-white/5 bg-[#141414] flex items-center px-5 shrink-0 gap-3">
        <span className="text-[10px] font-semibold text-zinc-500 tracking-widest uppercase">Veri Matrisi</span>
        <span className="text-[10px] text-emerald-400/80 font-mono bg-emerald-500/5 px-2 py-0.5 rounded border border-emerald-500/15 truncate max-w-[280px]">
          {fileName}
        </span>
      </div>

      {/* Scrollable Table */}
      <div ref={containerRef} className="flex-1 overflow-auto">
        {rows.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-zinc-600">
            <p className="text-sm italic">Görüntülenecek veri bulunamadı.</p>
          </div>
        ) : (
          <table className="w-full text-xs" style={{ tableLayout: 'fixed', borderCollapse: 'separate', borderSpacing: 0 }}>
            {/* Column widths */}
            <colgroup>
              <col style={{ width: 48 }} />
              {headers.map((_, i) => (
                <col key={i} style={{ width: 160 }} />
              ))}
            </colgroup>

            {/* Sticky Header */}
            <thead className="sticky top-0 z-10">
              <tr className="bg-[#161616] border-b border-white/8">
                <th className="px-3 py-2 text-center text-zinc-600 font-medium border-b border-white/5 select-none">
                  <span className="text-[10px]">#</span>
                </th>
                {headers.map((header, i) => (
                  <th
                    key={i}
                    className={`px-4 py-2 border-b border-white/5 font-medium text-zinc-300 whitespace-nowrap overflow-hidden text-ellipsis ${colAlign[colTypes[i]]}`}
                  >
                    <div className={`flex items-center gap-1.5 ${colTypes[i] === 'number' ? 'justify-end' : colTypes[i] === 'boolean' ? 'justify-center' : 'justify-start'}`}>
                      <TypeIcon type={colTypes[i]} />
                      <span className="truncate">{header}</span>
                    </div>
                  </th>
                ))}
              </tr>
            </thead>

            {/* Virtual Body */}
            <tbody>
              {/* Top spacer */}
              {virtualItems.length > 0 && virtualItems[0].start > 0 && (
                <tr style={{ height: virtualItems[0].start }}>
                  <td colSpan={headers.length + 1} />
                </tr>
              )}

              {virtualItems.map((vRow) => {
                const row = rows[vRow.index];
                const isOdd = vRow.index % 2 !== 0;
                return (
                  <tr
                    key={vRow.key}
                    ref={rowVirtualizer.measureElement}
                    data-index={vRow.index}
                    className={`group border-b border-white/[0.03] hover:bg-emerald-500/[0.04] transition-colors duration-75 ${isOdd ? 'bg-[#0e0e0e]' : 'bg-[#101010]'}`}
                    style={{ height: ROW_HEIGHT }}
                  >
                    {/* Row number */}
                    <td className="px-3 text-center text-zinc-700 font-mono text-[10px] select-none group-hover:text-zinc-500 transition-colors">
                      {vRow.index + 1}
                    </td>
                    {headers.map((_, ci) => (
                      <td
                        key={ci}
                        className={`px-4 overflow-hidden text-ellipsis whitespace-nowrap ${colAlign[colTypes[ci]]} ${colFontStyle[colTypes[ci]]}`}
                        title={row?.[ci] ?? ''}
                      >
                        {row?.[ci] || <span className="text-zinc-700">—</span>}
                      </td>
                    ))}
                  </tr>
                );
              })}

              {/* Bottom spacer */}
              {virtualItems.length > 0 && (
                <tr style={{ height: Math.max(0, totalSize - (virtualItems[virtualItems.length - 1]?.end ?? 0)) }}>
                  <td colSpan={headers.length + 1} />
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>

      {/* Status Bar */}
      <div className="h-6 border-t border-white/5 bg-[#141414] flex items-center px-5 gap-4 shrink-0">
        <span className="text-[10px] text-zinc-500 font-mono">
          Toplam{' '}
          <span className="text-zinc-300 font-semibold">{rows.length.toLocaleString()}</span>
          {' '}satır /{' '}
          <span className="text-zinc-300 font-semibold">{headers.length}</span>
          {' '}sütun
        </span>
        <span className="text-zinc-700">·</span>
        <span className="text-[10px] text-emerald-400/60 font-mono">⚡ Sanal Kaydırma</span>
        <span className="text-zinc-700">·</span>
        <span className="text-[10px] text-sky-400/60 font-mono">🔍 Tip Algılama</span>
      </div>
    </div>
  );
}
