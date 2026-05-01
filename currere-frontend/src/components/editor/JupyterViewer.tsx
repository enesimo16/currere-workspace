import { useState, useEffect, useCallback, useRef } from 'react';
import { DiPython } from 'react-icons/di';
import { FiPlay, FiPlus, FiTrash2, FiType, FiCode, FiRefreshCw, FiCheck, FiChevronUp, FiChevronDown } from 'react-icons/fi';
import api from '@/services/api';
import toast from 'react-hot-toast';

interface JupyterCell {
  cell_type: 'markdown' | 'code';
  source: string[];
  metadata: Record<string, unknown>;
  outputs?: unknown[];
  execution_count?: number | null;
}

interface JupyterNotebook {
  cells: JupyterCell[];
  metadata: Record<string, unknown>;
  nbformat: number;
  nbformat_minor: number;
}

interface CellOutput {
  text: string;
  isError: boolean;
  errorType?: string;
}

interface JupyterViewerProps {
  content: string;
  workspaceId: string | number;
  activeFileName: string;
}

export default function JupyterViewer({ content, workspaceId, activeFileName }: JupyterViewerProps) {
  const [notebook, setNotebook] = useState<JupyterNotebook | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cellOutputs, setCellOutputs] = useState<Record<number, CellOutput>>({});
  const [runningCell, setRunningCell] = useState<number | null>(null);
  const [focusedCell, setFocusedCell] = useState<number>(0);
  const [isKernelAlive, setIsKernelAlive] = useState(false);
  const saveTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const textareaRefs = useRef<Record<number, HTMLTextAreaElement | null>>({});

  // ── PARSE NOTEBOOK ──────────────────────────────────────────────────────
  useEffect(() => {
    try {
      if (!content?.trim()) {
        setNotebook({
          cells: [{ cell_type: 'code', source: [''], metadata: {}, outputs: [] }],
          metadata: {}, nbformat: 4, nbformat_minor: 5
        });
        return;
      }
      const parsed = JSON.parse(content);
      // source'ları normalize et (string → string[])
      if (parsed.cells) {
        parsed.cells = parsed.cells.map((c: JupyterCell) => ({
          ...c,
          source: Array.isArray(c.source) ? c.source : [c.source || ''],
          outputs: c.outputs || [],
          metadata: c.metadata || {}
        }));
      }
      setNotebook(parsed);
      setError(null);
    } catch {
      setError('Notebook formatı geçersiz veya bozuk.');
    }
  }, [content]);

  // ── KERNEL STATUS ───────────────────────────────────────────────────────
  useEffect(() => {
    const checkKernel = async () => {
      try {
        const res = await api.get(`/kernel/${workspaceId}/status`);
        setIsKernelAlive(res.data.isAlive);
      } catch { setIsKernelAlive(false); }
    };
    checkKernel();
  }, [workspaceId]);

  // ── NOTEBOOK KAYDET (Auto-save, FormData/Blob) ─────────────────────────
  const saveNotebook = useCallback(async (nb: JupyterNotebook) => {
    if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    saveTimeoutRef.current = setTimeout(async () => {
      try {
        const jsonStr = JSON.stringify(nb, null, 2);
        const blob = new Blob([jsonStr], { type: 'application/json' });
        const formData = new FormData();
        formData.append('file', blob, activeFileName);
        await api.put(`/workspace/${workspaceId}/file/${activeFileName}`, formData);
      } catch (err) {
        console.error('Notebook kayıt hatası:', err);
      }
    }, 800);
  }, [workspaceId, activeFileName]);

  // ── HÜCRE İÇERİĞİ DEĞİŞ ───────────────────────────────────────────────
  const updateCellSource = (idx: number, value: string) => {
    if (!notebook) return;
    const updated = { ...notebook };
    updated.cells = [...updated.cells];
    updated.cells[idx] = { ...updated.cells[idx], source: value.split('\n').map((l, i, arr) => i < arr.length - 1 ? l + '\n' : l) };
    setNotebook(updated);
    saveNotebook(updated);
  };

  // ── HÜCRE ÇALIŞTIR (Kernel API) ────────────────────────────────────────
  const executeCell = async (idx: number) => {
    if (!notebook || runningCell !== null) return;
    const cell = notebook.cells[idx];
    if (cell.cell_type !== 'code') return;

    const code = cell.source.join('');
    if (!code.trim()) return;

    setRunningCell(idx);
    setCellOutputs(prev => ({ ...prev, [idx]: { text: '', isError: false } }));

    try {
      const res = await api.post(`/kernel/${workspaceId}/execute`, { code });
      const data = res.data;
      setCellOutputs(prev => ({
        ...prev,
        [idx]: {
          text: data.success ? data.output : data.error,
          isError: !data.success,
          errorType: data.errorType
        }
      }));
      setIsKernelAlive(true);
    } catch (err: any) {
      const msg = err.response?.data?.error || 'Kernel bağlantı hatası';
      setCellOutputs(prev => ({
        ...prev,
        [idx]: { text: msg, isError: true, errorType: 'ConnectionError' }
      }));
    } finally {
      setRunningCell(null);
    }
  };

  // ── HÜCRE EKLE ─────────────────────────────────────────────────────────
  const addCell = (type: 'code' | 'markdown', afterIdx: number) => {
    if (!notebook) return;
    const newCell: JupyterCell = {
      cell_type: type,
      source: [''],
      metadata: {},
      outputs: type === 'code' ? [] : undefined
    };
    const updated = { ...notebook };
    updated.cells = [...updated.cells];
    updated.cells.splice(afterIdx + 1, 0, newCell);
    setNotebook(updated);
    setFocusedCell(afterIdx + 1);
    saveNotebook(updated);
  };

  // ── HÜCRE SİL ──────────────────────────────────────────────────────────
  const deleteCell = (idx: number) => {
    if (!notebook || notebook.cells.length <= 1) {
      toast.error('Son hücre silinemez');
      return;
    }
    const updated = { ...notebook };
    updated.cells = updated.cells.filter((_, i) => i !== idx);
    setNotebook(updated);
    setFocusedCell(Math.max(0, idx - 1));
    // İlgili çıktıyı da temizle
    setCellOutputs(prev => {
      const next = { ...prev };
      delete next[idx];
      return next;
    });
    saveNotebook(updated);
  };

  // ── HÜCRE TAŞI ─────────────────────────────────────────────────────────
  const moveCell = (idx: number, direction: 'up' | 'down') => {
    if (!notebook) return;
    const newIdx = direction === 'up' ? idx - 1 : idx + 1;
    if (newIdx < 0 || newIdx >= notebook.cells.length) return;
    const updated = { ...notebook };
    updated.cells = [...updated.cells];
    [updated.cells[idx], updated.cells[newIdx]] = [updated.cells[newIdx], updated.cells[idx]];
    setNotebook(updated);
    setFocusedCell(newIdx);
    saveNotebook(updated);
  };

  // ── KERNEL RESTART ─────────────────────────────────────────────────────
  const restartKernel = async () => {
    const toastId = toast.loading('Kernel yeniden başlatılıyor...');
    try {
      await api.post(`/kernel/${workspaceId}/restart`);
      setCellOutputs({});
      setIsKernelAlive(true);
      toast.success('Kernel yeniden başlatıldı. Tüm değişkenler sıfırlandı.', { id: toastId });
    } catch {
      toast.error('Kernel yeniden başlatılamadı.', { id: toastId });
    }
  };

  // ── TEXTAREA AUTO-RESIZE ───────────────────────────────────────────────
  const autoResize = (el: HTMLTextAreaElement | null) => {
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = el.scrollHeight + 'px';
  };

  // ── RENDER ─────────────────────────────────────────────────────────────
  if (error) {
    return (
      <div className="h-full flex items-center justify-center bg-[#1e1e1e] p-8">
        <div className="max-w-md w-full bg-red-500/10 border border-red-500/20 rounded-2xl p-6 text-center">
          <p className="text-red-400 text-sm font-medium mb-2">Hata Oluştu</p>
          <p className="text-zinc-400 text-xs leading-relaxed">{error}</p>
        </div>
      </div>
    );
  }

  if (!notebook) return null;

  return (
    <div className="h-full overflow-y-auto bg-[#0d0d0d] scroll-smooth">
      {/* Kernel Toolbar */}
      <div className="sticky top-0 z-20 bg-[#1a1a1a]/95 backdrop-blur-xl border-b border-zinc-800/50 px-4 py-2 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className={`flex items-center gap-2 px-3 py-1.5 rounded-md text-[10px] font-bold tracking-wider border shadow-sm ${
            isKernelAlive 
              ? 'bg-white/10 text-white border-white/20' 
              : 'bg-zinc-900 text-zinc-500 border-zinc-800'
          }`}>
            <span className={`w-2 h-2 rounded-full ${isKernelAlive ? 'bg-white shadow-[0_0_8px_rgba(255,255,255,0.8)] animate-pulse' : 'bg-zinc-700'}`} />
            {isKernelAlive ? 'KERNEL AKTİF' : 'KERNEL KAPALI'}
          </div>

          <span className="text-[11px] text-zinc-500 font-mono tracking-wider">{notebook.cells.length} HÜCRE</span>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => addCell('code', notebook.cells.length - 1)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-mono font-medium text-zinc-400 hover:text-white hover:bg-zinc-800 transition-all"
          >
            <FiPlus className="w-3.5 h-3.5" /> KOD
          </button>
          <button
            onClick={() => addCell('markdown', notebook.cells.length - 1)}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-mono font-medium text-zinc-400 hover:text-white hover:bg-zinc-800 transition-all"
          >
            <FiType className="w-3.5 h-3.5" /> MARKDOWN
          </button>
          <div className="w-px h-5 bg-zinc-800 mx-1" />
          <button
            onClick={restartKernel}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-mono font-medium text-zinc-400 hover:text-white hover:bg-zinc-800 transition-all"
          >
            <FiRefreshCw className="w-3.5 h-3.5" /> RESTART / START
          </button>
        </div>
      </div>

      {/* Cells */}
      <div className="max-w-5xl mx-auto py-8 px-4 md:px-8 space-y-6 pb-32">
        {notebook.cells.map((cell, idx) => {
          const isCode = cell.cell_type === 'code';
          const isFocused = focusedCell === idx;
          const isRunning = runningCell === idx;
          const output = cellOutputs[idx];
          const sourceText = cell.source.join('');

          return (
            <div
              key={idx}
              className={`group relative transition-all duration-200 rounded-xl border ${
                isFocused 
                  ? 'border-zinc-600 bg-[#121212] shadow-lg shadow-black/40'
                  : 'bg-[#0f0f11] border-zinc-800 hover:border-zinc-700'
              }`}
              onClick={() => setFocusedCell(idx)}
            >
              {isFocused && <div className="absolute left-0 top-0 bottom-0 w-[3px] bg-zinc-400 rounded-l-xl z-10" />}
              
              {/* Cell Header */}
              <div className="flex items-center justify-between px-4 py-2 border-b border-white/5 bg-black/20">
                <div className="flex items-center gap-2">
                  {isCode ? (
                    <span className="text-xs font-mono text-zinc-600">
                      [{idx + 1}]
                    </span>
                  ) : (
                    <span className="text-xs font-mono text-zinc-600">
                      [M]
                    </span>
                  )}
                  {isRunning && (
                    <span className="ml-2 flex items-center gap-1.5 text-xs font-mono text-zinc-400 animate-pulse">
                      <span className="w-2 h-2 border border-zinc-400 border-t-transparent rounded-full animate-spin" />
                      ÇALIŞIYOR...
                    </span>
                  )}
                </div>

                <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  {isCode && (
                    <button
                      onClick={(e) => { e.stopPropagation(); executeCell(idx); }}
                      disabled={isRunning}
                      className="p-1.5 rounded-md text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800 transition-all disabled:opacity-30"
                      title="Çalıştır (Ctrl+Enter)"
                    >
                      <FiPlay className="w-3.5 h-3.5" />
                    </button>
                  )}
                  <button onClick={() => moveCell(idx, 'up')} className="p-1.5 rounded-md text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-all" title="Yukarı Taşı">
                    <FiChevronUp className="w-3 h-3" />
                  </button>
                  <button onClick={() => moveCell(idx, 'down')} className="p-1.5 rounded-md text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-all" title="Aşağı Taşı">
                    <FiChevronDown className="w-3 h-3" />
                  </button>
                  <button onClick={() => deleteCell(idx)} className="p-1.5 rounded-md text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-all" title="Sil">
                    <FiTrash2 className="w-3 h-3" />
                  </button>
                </div>
              </div>

              {/* Cell Body — Editable Textarea */}
              <div className="px-4 py-2">
                <textarea
                  ref={(el) => { textareaRefs.current[idx] = el; if (el) autoResize(el); }}
                  value={sourceText}
                  onChange={(e) => { updateCellSource(idx, e.target.value); autoResize(e.target); }}
                  onKeyDown={(e) => {
                    // Ctrl+Enter → çalıştır
                    if (e.ctrlKey && e.key === 'Enter' && isCode) {
                      e.preventDefault();
                      executeCell(idx);
                    }
                  }}
                  spellCheck={false}
                  className="w-full resize-none outline-none py-2 font-mono text-[14px] leading-relaxed bg-transparent text-zinc-300 placeholder:text-zinc-600 placeholder:italic"
                  style={{ minHeight: '40px' }}
                  placeholder={isCode ? '# Kod yazın...' : 'Markdown yazın...'}
                />
              </div>

              {/* Cell Output */}
              {isCode && output && output.text && (
                <div className={`border-t px-4 py-3 rounded-b-xl text-[12px] font-mono leading-relaxed whitespace-pre-wrap ${
                  output.isError
                    ? 'bg-red-500/5 border-red-500/20 text-red-300'
                    : 'bg-[#0a0a0a] border-zinc-800/50 text-zinc-300'
                }`}>
                  {output.isError && output.errorType && (
                    <span className="text-[10px] font-bold text-red-500 tracking-wider block mb-1">{output.errorType}</span>
                  )}
                  {output.text}
                </div>
              )}

              {/* İsRunning Output Placeholder */}
              {isCode && isRunning && !output?.text && (
                <div className="border-t border-zinc-800/50 bg-[#0a0a0a] px-4 py-3 rounded-b-xl">
                  <div className="flex items-center gap-2 text-[11px] text-zinc-500">
                    <span className="w-3 h-3 border-2 border-zinc-600 border-t-zinc-300 rounded-full animate-spin" />
                    Çalıştırılıyor...
                  </div>
                </div>
              )}

              {/* Add Cell Between */}
              <div className="absolute -bottom-3.5 left-1/2 -translate-x-1/2 opacity-0 group-hover:opacity-100 transition-all z-20 flex gap-2">
                <button
                  onClick={() => addCell('code', idx)}
                  className="flex items-center gap-1 px-3 py-1 rounded-full text-[10px] font-mono font-bold text-zinc-300 bg-zinc-800 border border-zinc-600 hover:text-white hover:bg-zinc-700 hover:scale-105 shadow-xl transition-all"
                >
                  <FiPlus className="w-3 h-3" /> KOD
                </button>
                <button
                  onClick={() => addCell('markdown', idx)}
                  className="flex items-center gap-1 px-3 py-1 rounded-full text-[10px] font-mono font-bold text-zinc-300 bg-zinc-800 border border-zinc-600 hover:text-white hover:bg-zinc-700 hover:scale-105 shadow-xl transition-all"
                >
                  <FiType className="w-3 h-3" /> MD
                </button>
              </div>
            </div>
          );
        })}

        {notebook.cells.length === 0 && (
          <div className="text-center py-20">
            <p className="text-zinc-500 text-sm mb-4 italic">Bu notebook henüz bir hücre içermiyor.</p>
            <div className="flex gap-3 justify-center">
              <button onClick={() => addCell('code', -1)} className="px-4 py-2 rounded border border-zinc-800 text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 transition-colors text-xs font-mono">
                <FiCode className="inline mr-1.5" /> Kod Hücresi Ekle
              </button>
              <button onClick={() => addCell('markdown', -1)} className="px-4 py-2 rounded border border-zinc-800 text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 transition-colors text-xs font-mono">
                <FiType className="inline mr-1.5" /> Markdown Ekle
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
