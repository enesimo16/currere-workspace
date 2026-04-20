'use client';

import React from 'react';

interface CsvTableProps {
  csvData: string;
  fileName: string;
}

export default function CsvTable({ csvData, fileName }: CsvTableProps) {
  // Basit parseCSV fonksiyonu
  const parseCSV = (text: string) => {
    const lines = text.split('\n').filter(line => line.trim() !== '');
    if (lines.length === 0) return { headers: [], rows: [] };
    
    const headers = lines[0].split(',').map(h => h.trim());
    const rows = lines.slice(1).map(line => line.split(',').map(c => c.trim()));
    
    return { headers, rows };
  };

  const { headers, rows } = parseCSV(csvData);

  return (
    <div className="flex-1 flex flex-col bg-[#111111] overflow-hidden">
      {/* Table Header Info */}
      <div className="h-10 border-b border-[#2d2d2d] bg-[#1a1a1a] flex items-center px-4 shrink-0 justify-between">
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold text-zinc-500 tracking-widest uppercase">STATİK VERİ MATRİSİ</span>
          <span className="text-[10px] text-emerald-400 font-mono bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
            {fileName}
          </span>
        </div>
      </div>

      {/* Data Grid Area */}
      <div className="flex-1 overflow-auto custom-scrollbar">
        <table className="w-full text-left text-xs border-collapse border-spacing-0">
          <thead className="sticky top-0 z-10">
            <tr className="bg-[#2d2d2d] border-b border-[#3d3d3d]">
              {headers.map((header, idx) => (
                <th key={idx} className="px-4 py-3 font-bold text-white uppercase tracking-wider border-r border-[#3d3d3d] last:border-r-0 whitespace-nowrap">
                  {header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-[#2d2d2d]">
            {rows.map((row, rowIndex) => (
              <tr key={rowIndex} className="hover:bg-[#1a1a1a] transition-colors group">
                {row.map((cell, cellIndex) => (
                  <td key={cellIndex} className="px-4 py-2.5 text-zinc-400 border-r border-[#2d2d2d] last:border-r-0 whitespace-nowrap group-hover:text-zinc-200">
                    {cell}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        
        {rows.length === 0 && (
          <div className="flex flex-col items-center justify-center p-20 text-zinc-600 gap-4">
             <div className="w-12 h-12 rounded-full bg-[#1a1a1a] border border-[#2d2d2d] flex items-center justify-center animate-pulse">
                <span className="text-xl">📊</span>
             </div>
             <p className="italic text-sm">Görüntülenecek veri bulunamadı.</p>
          </div>
        )}
      </div>
    </div>
  );
}
