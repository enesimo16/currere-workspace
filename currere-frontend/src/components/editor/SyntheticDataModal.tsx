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

  const [columns, setColumns] = useState([{ name: '', type: '' }]);

  if (!isOpen) return null;

  const handleAddColumn = () => {
    setColumns([...columns, { name: '', type: '' }]);
  };

  const handleRemoveColumn = (index: number) => {
    const newCols = [...columns];
    newCols.splice(index, 1);
    setColumns(newCols);
  };

  const updateColumn = (index: number, field: 'name' | 'type', value: string) => {
    const newCols = [...columns];
    newCols[index][field] = value;
    setColumns(newCols);
  };

  const handleSentezle = async () => {
    if (!prompt.trim() && mode === 2) {
      toast.error('Gelişmiş AI modu için ne tür bir veri istediğinizi açıklayın.');
      return;
    }

    setLoading(true);
    const toastId = toast.loading('Veri kümesi oluşturuluyor...');

    try {
      const columnsString = columns.filter(c => c.name.trim() !== '').map(c => `${c.name.trim()}:${c.type.trim() || 'string'}`).join(',');

      const response = await api.post(`/workspace/${workspaceId}/data/generate`, {
        prompt: prompt.trim(),
        rowCount: rowCount,
        fileName: fileName.endsWith('.csv') ? fileName : `${fileName}.csv`,
        mode: mode,
        columns: columnsString
      });

      toast.success('Veri kümesi başarıyla oluşturuldu.', { id: toastId });
      
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
      const errMsg = err.response?.data?.error || 'Üretim işlemi başarısız oldu.';
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
      
      <div className="relative w-full max-w-lg bg-zinc-950/90 backdrop-blur-md border border-white/10 rounded-2xl shadow-2xl shadow-black/50 overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="relative p-6 border-b border-white/5 flex justify-between items-center shrink-0">
          <div className="flex items-center gap-3">
            <FiDatabase className="w-6 h-6 text-zinc-400" />
            <div>
              <h3 className="text-lg font-semibold text-zinc-100 tracking-wide">VERİ ÜRETİCİSİ</h3>
              <p className="text-xs font-light tracking-wider text-zinc-500 uppercase">Gelişmiş Veri Üretim Modülü</p>
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
        <div className="p-8 space-y-6 relative overflow-y-auto custom-scrollbar">
          {loading ? (
            <div className="py-12 flex flex-col items-center justify-center gap-6 animate-in zoom-in-95 duration-500">
              <div className="relative">
                <div className="w-20 h-20 border-4 border-zinc-700 border-t-zinc-400 rounded-full animate-spin" />
                <div className="absolute inset-0 flex items-center justify-center">
                  <FiDatabase className="w-8 h-8 text-zinc-500/50 animate-bounce" />
                </div>
              </div>
              <div className="text-center space-y-2">
                <p className="text-sm font-bold text-zinc-400 animate-pulse tracking-wide">
                  Kayıtlar işleniyor ve veritabanı oluşturuluyor...
                </p>
                <p className="text-[10px] text-zinc-500 uppercase tracking-[0.2em]">Lütfen bekleyin, yapılandırma sürüyor.</p>
              </div>
            </div>
          ) : (
            <>
              {/* Dynamic Columns */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest">Sütunlar</label>
                  <button onClick={handleAddColumn} className="text-[10px] text-emerald-500 hover:text-emerald-400 font-bold uppercase tracking-widest">+ Sütun Ekle</button>
                </div>
                {columns.map((col, index) => (
                  <div key={index} className="flex items-center gap-3">
                    <input 
                      type="text" 
                      placeholder="Sütun Adı (Örn: Yas)" 
                      value={col.name} 
                      onChange={(e) => updateColumn(index, 'name', e.target.value)}
                      className="flex-1 bg-zinc-900/50 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-200 outline-none focus:border-zinc-500/50 transition-all font-mono"
                    />
                    <input 
                      type="text" 
                      placeholder="Veri Tipi (Örn: Sayı, İsim, Email)" 
                      value={col.type} 
                      onChange={(e) => updateColumn(index, 'type', e.target.value)}
                      className="flex-1 bg-zinc-900/50 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-200 outline-none focus:border-zinc-500/50 transition-all font-mono"
                    />
                    {columns.length > 1 && (
                      <button onClick={() => handleRemoveColumn(index)} className="text-red-500 hover:text-red-400 transition-colors">
                        <FiX className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                ))}
              </div>

              {/* Prompt Input */}
              <div className="space-y-1.5">
                <label className="text-[10px] font-medium text-zinc-400 uppercase tracking-widest block">Veri Seti Tanımı (AI Modu İçin)</label>
                <textarea 
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="Örn: 18-65 yaş arası, İstanbul'da yaşayan, teknoloji harcaması yapan kullanıcılar..."
                  className="w-full h-24 bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-3 text-sm text-zinc-200 outline-none focus:border-zinc-500/50 focus:ring-1 focus:ring-zinc-500/50 transition-all resize-none placeholder:text-zinc-700"
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
                      max="10000"
                      value={rowCount}
                      onChange={(e) => setRowCount(Number(e.target.value))}
                      className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-zinc-500/50 focus:ring-1 focus:ring-zinc-500/50 transition-all font-mono"
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
                      className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-zinc-500/50 focus:ring-1 focus:ring-zinc-500/50 transition-all appearance-none cursor-pointer"
                    >
                      <option value={1} className="bg-zinc-950">Standard (Bogus/Hızlı)</option>
                      <option value={2} className="bg-zinc-950">Detailed (AI/Gerçekçi)</option>
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
                  className="w-full bg-zinc-900/50 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 outline-none focus:border-zinc-500/50 focus:ring-1 focus:ring-zinc-500/50 transition-all font-mono"
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
                  className="flex-[2] px-6 py-3 bg-zinc-800 hover:bg-zinc-700 text-zinc-100 rounded-lg text-xs font-medium transition-all shadow-lg shadow-black/20 flex items-center justify-center gap-2"
                >
                  <FiDatabase className="w-4 h-4" />
                  VERİ ÜRETMEYİ BAŞLAT
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
