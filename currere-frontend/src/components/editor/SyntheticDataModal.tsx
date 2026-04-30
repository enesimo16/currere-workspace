import { useState } from 'react';
import { FiX, FiZap, FiDatabase, FiSettings } from 'react-icons/fi';
import api from '@/services/api';
import toast from 'react-hot-toast';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

interface SyntheticDataModalProps {
  workspaceId: string | number;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function SyntheticDataModal({ workspaceId, isOpen, onClose, onSuccess }: SyntheticDataModalProps) {
  const { setActiveFile } = useWorkspaceStore();
  const [prompt, setPrompt] = useState('');
  const [rowCount, setRowCount] = useState(50);
  const [fileName, setFileName] = useState('synthetic_data.csv');
  const [mode, setMode] = useState<number>(1); // 1: FastAndFake (Standard), 2: ZeroShotRealistic (Detailed)
  const [loading, setLoading] = useState(false);

  if (!isOpen) return null;

  const handleSentezle = async () => {
    if (!prompt.trim()) {
      toast.error('Lütfen ne tür bir veri istediğinizi açıklayın.');
      return;
    }

    setLoading(true);
    const toastId = toast.loading('Veri fabrikası çalışıyor...');

    try {
      const response = await api.post(`/workspace/${workspaceId}/SyntheticData/generate`, {
        prompt: prompt.trim(),
        rowCount: rowCount,
        fileName: fileName.endsWith('.csv') ? fileName : `${fileName}.csv`,
        mode: mode,
        columns: "" // Backend prompt üzerinden otomatik belirleyecek
      });

      toast.success('Veri fabrikası başarıyla üretim yaptı!', { id: toastId });
      
      // Listeyi yenile
      onSuccess();
      
      // Yeni dosyaya odaklan
      if (response.data.fileId) {
        setActiveFile({ 
          id: response.data.fileId, 
          name: response.data.fileName, 
          type: 'file' 
        });
      }
      
      onClose();
    } catch (err: any) {
      const errMsg = err.response?.data?.error || 'Sentezleme işlemi başarısız oldu.';
      toast.error(errMsg, { id: toastId });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center p-4 animate-in fade-in duration-300">
      <div 
        className="absolute inset-0 bg-black/70 backdrop-blur-md" 
        onClick={() => !loading && onClose()}
      />
      
      <div className="relative w-full max-w-lg bg-zinc-950/90 backdrop-blur-md border border-white/10 rounded-2xl shadow-2xl shadow-black/50 overflow-hidden">
        {/* Header */}
        <div className="relative p-6 border-b border-white/5 flex justify-between items-center">
          <div className="flex items-center gap-3">
            <FiDatabase className="w-6 h-6 text-emerald-400" />
            <div>
              <h3 className="text-lg font-semibold text-zinc-100 tracking-wide">VERİ FABRİKASI</h3>
              <p className="text-xs font-light tracking-wider text-zinc-500 uppercase">Otonom Sentetik Veri Sentezleyici</p>
            </div>
          </div>
          <button 
            onClick={onClose}
            className="p-2 text-zinc-500 hover:text-white hover:bg-white/5 rounded-lg transition-all"
            disabled={loading}
          >
            <FiX className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-8 space-y-6 relative">
          {loading ? (
            <div className="py-12 flex flex-col items-center justify-center gap-6 animate-in zoom-in-95 duration-500">
              <div className="relative">
                <div className="w-20 h-20 border-4 border-emerald-500/10 border-t-emerald-500 rounded-full animate-spin" />
                <div className="absolute inset-0 flex items-center justify-center">
                  <FiDatabase className="w-8 h-8 text-emerald-500/50 animate-bounce" />
                </div>
              </div>
              <div className="text-center space-y-2">
                <p className="text-sm font-bold text-emerald-400 animate-pulse tracking-wide">
                  Yapay zeka verileri moleküllerine ayırıyor ve sentezliyor...
                </p>
                <p className="text-[10px] text-zinc-500 uppercase tracking-[0.2em]">Lütfen bekleyin, fabrikada üretim sürüyor.</p>
              </div>
            </div>
          ) : (
            <>
              {/* Prompt Input */}
              <div className="space-y-1.5">
                <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest block">Veri Seti Tanımı</label>
                <textarea 
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="Örn: 18-65 yaş arası, İstanbul'da yaşayan, teknoloji harcaması yapan kullanıcılar..."
                  className="w-full h-32 bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-3 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 focus:ring-1 focus:ring-emerald-500/50 transition-all resize-none placeholder:text-zinc-700"
                />
              </div>

              <div className="grid grid-cols-2 gap-6">
                {/* Row Count */}
                <div className="space-y-1.5">
                  <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest block">Satır Sayısı</label>
                  <div className="relative">
                    <input 
                      type="number" 
                      min="1"
                      max="1000"
                      value={rowCount}
                      onChange={(e) => setRowCount(Number(e.target.value))}
                      className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 focus:ring-1 focus:ring-emerald-500/50 transition-all font-mono"
                    />
                    <span className="absolute right-4 top-1/2 -translate-y-1/2 text-[10px] font-bold text-zinc-600">ROWS</span>
                  </div>
                </div>

                {/* Mode Select */}
                <div className="space-y-1.5">
                  <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest block">Üretim Modu</label>
                  <div className="relative">
                    <select 
                      value={mode}
                      onChange={(e) => setMode(Number(e.target.value))}
                      className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 focus:ring-1 focus:ring-emerald-500/50 transition-all appearance-none cursor-pointer"
                    >
                      <option value={1} className="bg-zinc-950">Standard (Hızlı)</option>
                      <option value={2} className="bg-zinc-950">Detailed (Gerçekçi)</option>
                    </select>
                    <FiSettings className="absolute right-4 top-1/2 -translate-y-1/2 text-zinc-600 pointer-events-none" />
                  </div>
                </div>
              </div>

              {/* Filename Input */}
              <div className="space-y-1.5">
                <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest block">Kaydedilecek Dosya Adı</label>
                <input 
                  type="text" 
                  value={fileName}
                  onChange={(e) => setFileName(e.target.value)}
                  className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 focus:ring-1 focus:ring-emerald-500/50 transition-all font-mono"
                />
              </div>

              {/* Actions */}
              <div className="pt-4 flex gap-4">
                <button 
                  onClick={onClose}
                  className="flex-1 px-6 py-3 bg-transparent text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 border border-zinc-800 rounded-lg text-xs font-medium transition-all"
                >
                  İPTAL
                </button>
                <button 
                  onClick={handleSentezle}
                  className="flex-[2] px-6 py-3 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-medium transition-all shadow-lg shadow-emerald-900/20 flex items-center justify-center gap-2"
                >
                  <FiDatabase className="w-4 h-4" />
                  SENTEZLEMEYİ BAŞLAT
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
