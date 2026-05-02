import { useEffect, useState, useRef, useCallback } from 'react';
import api from '@/services/api';
import { FiUpload, FiFile, FiImage, FiPlus, FiSettings, FiSearch, FiDownload, FiX, FiEdit, FiTrash2, FiList, FiGrid, FiChevronDown, FiArrowRight, FiBox, FiLoader, FiFileText, FiDatabase, FiClock, FiFilePlus, FiArchive } from 'react-icons/fi';
import { DiPython } from 'react-icons/di';
import { BsFiletypeCsv, BsFiletypeJson, BsFiletypeSql } from 'react-icons/bs';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import { useAuthStore } from '@/store/useAuthStore';
import toast from 'react-hot-toast';
import SyntheticDataModal from './SyntheticDataModal';
import { FiZap } from 'react-icons/fi';

interface WorkspaceFile {
  id: number;
  fileName: string;
  uploadedAt: string;
  expiresAt: string;
}

interface KaggleResult {
  ref: string;
  title: string;
  size?: string;
}

interface FileExplorerProps {
  workspaceId: string | number;
}

const FileItem = ({ 
  file, 
  isMain = false, 
  level = 0, 
  activeFileName,
  setActiveFile,
  addOpenFile,
  renamingFileId,
  setRenamingFileId,
  newName,
  setNewName,
  handleRename,
  handleDeleteRequest,
  handleDownloadFile,
  getFileIcon
}: { 
  file: any; 
  isMain?: boolean; 
  level?: number;
  activeFileName: string;
  setActiveFile: (f: any) => void;
  addOpenFile?: (f: any) => void;
  renamingFileId: number | null;
  setRenamingFileId: (id: number | null) => void;
  newName: string;
  setNewName: (name: string) => void;
  handleRename: (id: number, oldName: string) => void;
  handleDeleteRequest: (name: string) => void;
  handleDownloadFile: (name: string) => void;
  getFileIcon: (name: string) => React.ReactNode;
}) => {
  const fileName = isMain ? 'main.py' : file.fileName;
  const isActive = activeFileName === fileName;
  const isRenaming = !isMain && renamingFileId === file.id;

  const bgClass = isActive ? 'bg-zinc-900/50 text-zinc-100' : 'text-zinc-400 hover:bg-zinc-900/30 hover:text-zinc-200';

  return (
    <div 
      title={fileName}
      onClick={() => {
         if (!isRenaming) {
           const newFile = { 
             id: isMain ? null : file.id,
             name: fileName, 
             type: fileName.endsWith('.csv') || fileName.endsWith('.xlsx') || fileName.endsWith('.txt') ? 'file' : 'code' 
           };
           setActiveFile(newFile);
           if (addOpenFile) addOpenFile(newFile);
         }
      }}
      style={{ paddingLeft: `${level === 0 ? 28 : 44}px` }}
      className={`group flex items-center gap-1.5 py-1 pr-2 cursor-pointer text-[13px] font-mono transition-colors ${bgClass}`}
    >
      <div className="shrink-0 flex items-center justify-center w-[18px] h-[18px]">
        {isMain ? <DiPython className="text-[#60a5fa] w-full h-full" /> : getFileIcon(fileName)}
      </div>
      
      {isRenaming ? (
        <input
          autoFocus
          className="flex-1 min-w-0 bg-zinc-950 text-zinc-100 text-[13px] px-1 py-0 border border-emerald-500/50 outline-none rounded-sm"
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleRename(file.id, file.fileName);
            if (e.key === 'Escape') setRenamingFileId(null);
          }}
          onBlur={() => handleRename(file.id, file.fileName)}
          onClick={(e) => e.stopPropagation()}
        />
      ) : (
        <span className={`truncate flex-1 ${isActive ? 'font-medium' : ''}`}>
          {fileName}
        </span>
      )}

      <div className="hidden group-hover:flex items-center gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
        {!isMain && (
          <button 
            onClick={(e) => { e.stopPropagation(); setRenamingFileId(file.id); setNewName(file.fileName); }}
            className="p-1 hover:bg-zinc-800 text-zinc-500 hover:text-zinc-200 rounded transition-colors"
            title="Yeniden Adlandır"
          >
            <FiEdit className="w-3.5 h-3.5" />
          </button>
        )}
        <button 
          onClick={(e) => { e.stopPropagation(); handleDownloadFile(fileName); }}
          className="p-1 hover:bg-slate-200 text-slate-400 hover:text-slate-700 rounded transition-colors"
          title="İndir"
        >
          <FiDownload className="w-3.5 h-3.5" />
        </button>
        {!isMain && (
          <button 
            onClick={(e) => { e.stopPropagation(); handleDeleteRequest(file.fileName); }}
            className="p-1 hover:bg-red-50 text-slate-400 hover:text-red-500 rounded transition-colors"
            title="Sil"
          >
            <FiTrash2 className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
    </div>
  );
};

const FolderGroup = ({ title, icon, files, isOpen, onToggle, fileProps }: { title: string, icon: string, files: any[], isOpen: boolean, onToggle: () => void, fileProps: any }) => {
  if (files.length === 0) return null;

  // Alfabetik sıralama
  const sortedFiles = [...files].sort((a, b) => a.fileName.localeCompare(b.fileName));

  return (
    <div className="flex flex-col">
      <div 
        onClick={onToggle}
        className="flex items-center gap-1 py-1.5 pr-2 pl-2 text-[11px] font-mono font-bold text-zinc-500 hover:bg-zinc-900/30 hover:text-zinc-300 cursor-pointer select-none transition-colors uppercase tracking-widest"
      >
        <FiChevronDown className={`w-3.5 h-3.5 transition-transform ${isOpen ? '' : '-rotate-90'}`} />
        <span className="ml-0.5">{title}</span>
      </div>
      
      {isOpen && (
        <div className="flex flex-col">
          {sortedFiles.map((file: any) => (
            <div key={file.id}>
              <FileItem file={file} {...fileProps} level={1} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default function FileExplorer({ workspaceId }: FileExplorerProps) {
  const { activeWorkspace, activeFile, setActiveFile, viewMode, setViewMode, addOpenFile, isKaggleOpen, setKaggleOpen, isHistoryOpen, setHistoryOpen, isSyntheticOpen, setSyntheticOpen } = useWorkspaceStore();
  const { huggingFaceToken, setHuggingFaceToken } = useAuthStore();
  const [files, setFiles] = useState<WorkspaceFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isCreatingFile, setIsCreatingFile] = useState(false);
  const [isExtMenuOpen, setIsExtMenuOpen] = useState(false);
  const [newFileName, setNewFileName] = useState('');
  const [newFileExt, setNewFileExt] = useState('.py');
  const [snapshots, setSnapshots] = useState<any[]>([]);
  const [isTakingSnapshot, setIsTakingSnapshot] = useState(false);
  const [isRestoring, setIsRestoring] = useState(false);
  const [isSnapModalOpen, setIsSnapModalOpen] = useState(false);
  const [snapName, setSnapName] = useState('');
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isSyntheticModalOpen, setIsSyntheticModalOpen] = useState(false);
  const [kaggleUsername, setKaggleUsername] = useState('');
  const [kaggleKey, setKaggleKey] = useState('');
  const [isKaggleConfigured, setIsKaggleConfigured] = useState(false);
  const [hfTokenInput, setHfTokenInput] = useState(huggingFaceToken || '');
  const [isSavingSettings, setIsSavingSettings] = useState(false);
  const [kaggleSearch, setKaggleSearch] = useState('');
  const [kaggleResults, setKaggleResults] = useState<KaggleResult[]>([]);
  const [isSearchingKaggle, setIsSearchingKaggle] = useState(false);
  const [downloadingDataset, setDownloadingDataset] = useState<string | null>(null);
  const [renamingFileId, setRenamingFileId] = useState<number | null>(null);
  const [newName, setNewName] = useState('');
  const [fileToDelete, setFileToDelete] = useState<string | null>(null);

  const [openGroups, setOpenGroups] = useState({
    source: true,
    data: true,
    notebook: true,
    other: true
  });

  const fileInputRef = useRef<HTMLInputElement>(null);

  const fetchFiles = useCallback(async () => {
    try {
      setLoading(true);
      const response = await api.get(`/workspace/${workspaceId}/file`);
      setFiles(response.data);
    } catch {
      console.error('Dosyalar getirilirken hata oluştu');
    } finally {
      setLoading(false);
    }
  }, [workspaceId]);

  const fetchSnapshots = useCallback(async () => {
    try {
      const res = await api.get(`/workspace/${workspaceId}/snapshot`);
      setSnapshots(res.data);
    } catch {
      console.error('Yedekler getirilemedi');
    }
  }, [workspaceId]);

  useEffect(() => {
    if (workspaceId) {
       fetchFiles();
       fetchSnapshots();
    }
  }, [workspaceId, fetchFiles, fetchSnapshots]);

  useEffect(() => {
    if (isSettingsOpen) {
      const fetchKaggleSettings = async () => {
        try {
          const res = await api.get('/user/settings/kaggle');
          if (res.data) {
            if (res.data.username) {
              setKaggleUsername(res.data.username);
            }
            if (res.data.isConfigured) {
              setIsKaggleConfigured(true);
            } else {
              setIsKaggleConfigured(false);
            }
          }
        } catch (error) {
          console.error("Kaggle ayarları getirilemedi:", error);
        }
      };
      fetchKaggleSettings();
    }
  }, [isSettingsOpen]);

  const handleFileUploadClick = () => {
    if (!isUploading) fileInputRef.current?.click();
  };

  const handleFileCreate = async () => {
    if (!newFileName.trim() || !workspaceId) {
      setIsCreatingFile(false);
      return;
    }
    
    const fullName = newFileName.includes('.') ? newFileName.trim() : `${newFileName.trim()}${newFileExt}`;
    
    try {
      setIsUploading(true);
      const res = await api.post(`/workspace/${workspaceId}/file/create`, { fileName: fullName });
      
      // Jupyter Notebook iskeleti başlatma
      if (fullName.endsWith('.ipynb')) {
        const skeleton = JSON.stringify({
          cells: [{ cell_type: "markdown", source: ["# Yeni Notebook\nKod yazmaya hazır!"] }],
          metadata: {},
          nbformat: 4,
          nbformat_minor: 5
        });
        
        const blob = new Blob([skeleton], { type: 'text/plain' });
        const formData = new FormData();
        formData.append('file', blob, fullName);
        await api.put(`/workspace/${workspaceId}/file/${fullName}`, formData, {
          headers: { 'Content-Type': 'multipart/form-data' }
        });
      }

      await fetchFiles();
      const newFile = { 
        id: res.data.fileId,
        name: fullName, 
        type: fullName.endsWith('.csv') || fullName.endsWith('.xlsx') || fullName.endsWith('.txt') ? 'file' : 'code' 
      };
      setActiveFile(newFile);
      addOpenFile(newFile);
      setNewFileName('');
      setIsCreatingFile(false);
      toast.success('Dosya oluşturuldu');
    } catch {
      toast.error('Dosya oluşturulamadı.');
    } finally {
      setIsUploading(false);
    }
  };

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !workspaceId) return;
    const toastId = toast.loading('Dosya yükleniyor...');
    try {
      setIsUploading(true);
      const formData = new FormData();
      formData.append('file', file);
      await api.post(`/workspace/${workspaceId}/file/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      if (fileInputRef.current) fileInputRef.current.value = '';
      await fetchFiles();
      toast.success('Dosya yüklendi.', { id: toastId });
    } catch {
      toast.error('Dosya yüklenemedi.', { id: toastId });
    } finally {
      setIsUploading(false);
    }
  };

  const handleSaveSettings = async () => {
    try {
      setIsSavingSettings(true);
      // Kaggle ayarları backend'e
      await api.post('/user/settings/kaggle', { username: kaggleUsername, key: kaggleKey });
      
      // HF Token Zustand (ve dolayısıyla localStorage)
      setHuggingFaceToken(hfTokenInput);
      
      setIsSettingsOpen(false);
      toast.success('Ayarlar kaydedildi');
    } catch {
      toast.error('Ayarlar kaydedilirken hata oluştu');
    } finally {
      setIsSavingSettings(false);
    }
  };

  const handleKaggleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!kaggleSearch.trim()) return;

    setIsSearchingKaggle(true);
    try {
      // Backend: GET /api/workspace/{id}/Kaggle/search?query=...
      const response = await api.get(`/workspace/${workspaceId}/Kaggle/search?query=${encodeURIComponent(kaggleSearch.trim())}`);
      setKaggleResults(response.data);
    } catch (error: any) {
      console.error('Kaggle search error:', error);
      toast.error('Kaggle araması başarısız oldu.');
    } finally {
      setIsSearchingKaggle(false);
    }
  };

  const handleTakeSnapshot = async () => {
    const toastId = toast.loading('Çalışma alanı yedeği alınıyor...');
    try {
      setIsTakingSnapshot(true);
      setIsSnapModalOpen(false);

      // Eğer isim girilmemişse tarih/saat kullan
      const finalName = snapName.trim() || 'Manuel Yedek - ' + new Date().toLocaleTimeString('tr-TR');

      // DTO: Label (kullanıcı etiketi) + Description
      const response = await api.post(`/workspace/${workspaceId}/snapshot`, { 
        Label: finalName,
        Description: finalName
      });
      
      toast.success(response.data?.message || 'Yedek başarıyla alındı.', { id: toastId });
      setSnapName(''); // İsmi temizle
      // Listeyi anında tazele
      await fetchSnapshots();
    } catch (error: any) {
      // Backend'den gelen gerçek hata mesajını yakala (BadRequest error: ex.Message)
      const serverError = error.response?.data?.error || error.response?.data?.message || 'Yedek alınamadı.';
      toast.error(serverError, { id: toastId });
      console.error('Snapshot Error:', error.response?.data);
    } finally {
      setIsTakingSnapshot(false);
    }
  };

  const handleRestoreSnapshot = async (snapshotId: number) => {
    const toastId = toast.loading('Geçmişe dönülüyor...');
    try {
      setIsRestoring(true);
      await api.post(`/workspace/${workspaceId}/snapshot/${snapshotId}/restore`);
      
      // KRITIK: Sayfa yenilenmeden önce yerel store'u (localStorage) güncellemeliyiz.
      // Yoksa auto-save mekanizması eski kodu veritabanına geri yazar (Race Condition).
      const workspaceResponse = await api.get(`/workspace/${workspaceId}`);
      if (workspaceResponse.data) {
        useWorkspaceStore.getState().setActiveWorkspace(workspaceResponse.data);
      }

      toast.success('Geçmişe başarıyla dönüldü!', { id: toastId });
      
      // Dosyaları ve varsa içeriği yenile
      await fetchFiles();
      await fetchSnapshots();

      // En sağlıklı yöntem tüm state'i temizleyip yeniden yüklemek (Dosya ağacı vb. için)
      setTimeout(() => {
        window.location.reload();
      }, 500);
    } catch (error: any) {
      const serverError = error.response?.data?.error || 'Geri yükleme başarısız.';
      toast.error(serverError, { id: toastId });
    } finally {
      setIsRestoring(false);
    }
  };

  const handleDeleteSnapshot = async (snapshotId: number) => {
    if (!window.confirm('Bu yedeği silmek istediğinize emin misiniz? Bu işlem geri alınamaz.')) return;

    const toastId = toast.loading('Yedek siliniyor...');
    try {
      await api.delete(`/workspace/${workspaceId}/snapshot/${snapshotId}`);
      toast.success('Yedek başarıyla silindi.', { id: toastId });
      await fetchSnapshots();
    } catch (error: any) {
      const serverError = error.response?.data?.error || 'Yedek silinemedi.';
      toast.error(serverError, { id: toastId });
    }
  };

  const handleKaggleDownload = async (datasetRef: string) => {
    if (!workspaceId) return;
    const toastId = toast.loading(`${datasetRef} indiriliyor...`);
    try {
      setDownloadingDataset(datasetRef);
      await api.post(`/workspace/${workspaceId}/Kaggle/download`, { datasetRef });
      toast.success('Veri seti başarıyla çalışma alanına eklendi.', { id: toastId });
      await fetchFiles();
      setActiveTab('files');
    } catch (error: any) {
      const serverError = error.response?.data?.error || 'İndirme başarısız oldu.';
      toast.error(serverError, { id: toastId });
    } finally {
      setDownloadingDataset(null);
    }
  };

  const handleRename = async (id: number, oldName: string) => {
    if (!newName.trim() || newName === oldName) {
      setRenamingFileId(null);
      return;
    }
    try {
      await api.put(`/workspace/${workspaceId}/file/${oldName}/rename`, { newFileName: newName.trim() });
      if (activeFile.name === oldName) {
        setActiveFile({ ...activeFile, name: newName.trim() });
      }
      await fetchFiles();
      setRenamingFileId(null);
      toast.success('Yeniden adlandırıldı');
    } catch {
      toast.error('Dosya adı değiştirilemedi.');
    }
  };

  const handleDeleteFile = async () => {
    if (!fileToDelete) return;
    try {
      await api.delete(`/workspace/${workspaceId}/file/${fileToDelete}`);
      if (activeFile.name === fileToDelete) {
        setActiveFile({ id: null, name: 'main.py', type: 'code' });
      }
      await fetchFiles();
      setFileToDelete(null);
      toast.success('Dosya silindi');
    } catch {
      toast.error('Dosya silinemedi.');
    }
  };

  const handleDownloadFile = (fileName: string) => {
    try {
      if (fileName !== activeFile.name) {
         toast.error('Sadece şu an açık olan (aktif) dosyayı indirebilirsiniz. Diğerleri için ZIP kullanın.');
         return;
      }

      let fileContent = '';

      if (typeof window !== 'undefined' && (window as any).monaco) {
        const models = (window as any).monaco.editor.getModels();
        if (models && models.length > 0) {
          fileContent = models[0].getValue();
        }
      }

      // Dosya içeriği boş olsa bile (0 byte) Frontend üzerinden indirme işlemine izin ver
      const blob = new Blob([fileContent || ""], { type: 'text/plain' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      toast.success(`${fileName} indirildi.`);
    } catch {
      toast.error('Dosya indirilemedi.');
    }
  };

  const handleDownloadWorkspaceZip = async () => {
    if (!workspaceId) return;
    const toastId = toast.loading('ZIP hazırlanıyor...');
    try {
      // api.get zaten axios instance'ı olduğu için Header'a Bearer token'ı otomatik ekler.
      // responseType: 'blob' ayarını KESİNLİKLE ekliyoruz.
      const response = await api.get(`/workspace/${workspaceId}/export`, {
        responseType: 'blob'
      });

      const url = window.URL.createObjectURL(new Blob([response.data]));
      const a = document.createElement('a');
      a.href = url;
      a.download = `workspace_${workspaceId}_export.zip`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      toast.success('Tüm dosyalar indirildi!', { id: toastId });
    } catch {
      toast.error('İndirme başarısız oldu.', { id: toastId });
    }
  };

  const getFileIcon = (fileName: string) => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'py': return <DiPython className="text-[#60a5fa] w-full h-full" />;
      case 'js': case 'ts': return <FiFile className="text-[#fbbf24] w-full h-full" />;
      case 'html': return <FiFile className="text-[#fb923c] w-full h-full" />;
      case 'css': return <FiFile className="text-[#60a5fa] w-full h-full" />;
      case 'ipynb': return <FiFile className="text-[#fb923c] w-full h-full" />;
      case 'csv': case 'xlsx': return <BsFiletypeCsv className="text-[#34d399] w-full h-full" />;
      case 'json': return <BsFiletypeJson className="text-[#fbbf24] w-full h-full" />;
      case 'sql': return <BsFiletypeSql className="text-[#fb7185] w-full h-full" />;
      case 'png': case 'jpg': case 'jpeg': return <FiImage className="text-[#c084fc] w-full h-full" />;
      default: return <FiFile className="text-[#94a3b8] w-full h-full" />;
    }
  };
  return (
    <>
    <div className="w-full h-full bg-[#0c0c0e] border-r border-zinc-800/50 flex flex-col shrink-0 transition-all duration-300">
      <input type="file" className="hidden" ref={fileInputRef} onChange={handleFileChange} accept=".csv,.xlsx,.json,.txt,.py,.ipynb" />
      
      <div className="flex flex-col border-b border-zinc-800/50 shrink-0 text-zinc-400">
        <div className="px-4 py-4 flex items-center justify-between">
          <span className="text-[10px] font-black text-zinc-500 tracking-[0.2em] uppercase">ÇALIŞMA ALANI DOSYALARI</span>
          <div className="flex items-center gap-3">
             <button onClick={() => setIsCreatingFile(!isCreatingFile)} className="text-zinc-500 hover:text-zinc-100 transition-colors" title="Yeni Dosya">
                <FiFilePlus className="w-3.5 h-3.5" />
             </button>
             <button onClick={handleFileUploadClick} className="text-zinc-500 hover:text-zinc-100 transition-colors" title="Dosya Yükle">
                <FiUpload className="w-3.5 h-3.5" />
             </button>
             <button 
                onClick={() => setViewMode(viewMode === 'list' ? 'tree' : 'list')} 
                className="text-zinc-500 hover:text-zinc-100 transition-colors" 
                title={viewMode === 'list' ? 'Ağaç Görünümüne Geç' : 'Liste Görünümüne Geç'}
             >
                {viewMode === 'list' ? <FiGrid className="w-3.5 h-3.5" /> : <FiList className="w-3.5 h-3.5" />}
             </button>
             <button onClick={handleDownloadWorkspaceZip} className="text-zinc-500 hover:text-zinc-100 transition-colors" title="Tümünü İndir (ZIP)">
                <FiDownload className="w-3.5 h-3.5" />
             </button>
          </div>
        </div>

        {isCreatingFile && (
          <div className="px-3 pb-3 flex flex-col gap-2">
            <div className="flex items-center gap-1 bg-zinc-900/50 border border-zinc-800/50 rounded-md px-1 py-0.5 shadow-sm">
               <input type="text" autoFocus value={newFileName} onChange={(e) => setNewFileName(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleFileCreate()} placeholder="Dosya adı..." className="flex-1 bg-transparent text-zinc-100 text-xs px-2 py-1 outline-none font-mono" />
               <div className="relative">
                  <button 
                    onMouseDown={(e) => { e.stopPropagation(); setIsExtMenuOpen(!isExtMenuOpen); }}
                    className="flex items-center gap-1 text-[10px] bg-zinc-800 hover:bg-zinc-700 text-zinc-300 px-1.5 py-1 rounded transition-colors font-bold uppercase"
                  >
                    {newFileExt.replace('.', '')}
                    <FiChevronDown className={`w-3 h-3 transition-transform ${isExtMenuOpen ? 'rotate-180' : ''}`} />
                  </button>
                  {isExtMenuOpen && (
                    <div className="absolute right-0 top-full mt-1 bg-zinc-900 border border-zinc-800 rounded shadow-lg z-50 min-w-[100px] overflow-hidden animate-in fade-in slide-in-from-top-1 duration-200">
                      <button 
                        onMouseDown={() => { setNewFileExt('.py'); setIsExtMenuOpen(false); }}
                        className="w-full text-left px-3 py-2 text-[10px] hover:bg-zinc-800 hover:text-emerald-500 transition-colors border-b border-zinc-800/50 font-bold text-zinc-400"
                      >
                        PYTHON (.py)
                      </button>
                      <button 
                        onMouseDown={() => { setNewFileExt('.ipynb'); setIsExtMenuOpen(false); }}
                        className="w-full text-left px-3 py-2 text-[10px] hover:bg-zinc-800 hover:text-emerald-500 transition-colors font-bold text-zinc-400"
                      >
                        NOTEBOOK (.ipynb)
                      </button>
                    </div>
                  )}
               </div>
            </div>
          </div>
        )}
      </div>

      <div className="flex-1 overflow-y-auto py-2">
            {loading ? (
              <div className="px-4 py-3 text-xs text-zinc-500 hidden md:block animate-pulse">Yükleniyor...</div>
            ) : (
              <div className="flex flex-col gap-px pb-4">
                
                {/* Ana Dosya (main.py) daima en üstte */}
                <FileItem 
                  file={{}} 
                  isMain={true} 
                  level={0} 
                  activeFileName={activeFile.name}
                  setActiveFile={setActiveFile}
                  addOpenFile={addOpenFile}
                  renamingFileId={renamingFileId}
                  setRenamingFileId={setRenamingFileId}
                  newName={newName}
                  setNewName={setNewName}
                  handleRename={handleRename}
                  handleDeleteRequest={() => toast.error('Ana dosya silinemez.')}
                  handleDownloadFile={handleDownloadFile}
                  getFileIcon={getFileIcon}
                />

                {(() => {
                  const sourceExts = ['py', 'js', 'ts', 'cs', 'html', 'css'];
                  const dataExts = ['csv', 'json', 'xlsx', 'txt', 'sql'];
                  
                  const sourceFiles = files.filter(f => sourceExts.includes(f.fileName.split('.').pop()?.toLowerCase() || ''));
                  const dataFiles = files.filter(f => dataExts.includes(f.fileName.split('.').pop()?.toLowerCase() || ''));
                  const notebookFiles = files.filter(f => f.fileName.endsWith('.ipynb'));
                  
                  const groupedExts = [...sourceExts, ...dataExts, 'ipynb'];
                  const otherFiles = files.filter(f => !groupedExts.includes(f.fileName.split('.').pop()?.toLowerCase() || ''));

                  const fileProps = {
                    activeFileName: activeFile.name,
                    setActiveFile,
                    addOpenFile,
                    renamingFileId,
                    setRenamingFileId,
                    newName,
                    setNewName,
                    handleRename,
                    handleDeleteRequest: (name: string) => setFileToDelete(name),
                    handleDownloadFile,
                    getFileIcon
                  };

                  return (
                    <div className="mt-1 flex flex-col gap-1">
                      <FolderGroup 
                        title="Source" icon="📂" files={sourceFiles} 
                        isOpen={openGroups.source} onToggle={() => setOpenGroups(p => ({ ...p, source: !p.source }))}
                        fileProps={fileProps}
                      />
                      <FolderGroup 
                        title="Data" icon="📊" files={dataFiles} 
                        isOpen={openGroups.data} onToggle={() => setOpenGroups(p => ({ ...p, data: !p.data }))}
                        fileProps={fileProps}
                      />
                      <FolderGroup 
                        title="Notebooks" icon="📓" files={notebookFiles} 
                        isOpen={openGroups.notebook} onToggle={() => setOpenGroups(p => ({ ...p, notebook: !p.notebook }))}
                        fileProps={fileProps}
                      />
                      <FolderGroup 
                        title="Others" icon="📄" files={otherFiles} 
                        isOpen={openGroups.other} onToggle={() => setOpenGroups(p => ({ ...p, other: !p.other }))}
                        fileProps={fileProps}
                      />
                    </div>
                  );
                })()}

              </div>
            )}
      </div>

      <div className="mt-auto p-3 border-t border-zinc-800/50 flex justify-center md:justify-start bg-[#0c0c0e] relative z-10">
        <button onClick={() => setIsSettingsOpen(true)} className="flex items-center gap-2 text-zinc-400 hover:text-zinc-100 transition-colors w-full p-2 rounded-lg hover:bg-zinc-800/50 border border-transparent hover:border-zinc-700 shadow-sm">
          <FiSettings className="w-4 h-4 shrink-0" />
          <span className="hidden md:block text-[11px] font-bold tracking-widest uppercase">Ayarlar</span>
        </button>
      </div>

      {/* Delete Confirmation Modal */}
      {fileToDelete && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-zinc-950/40 backdrop-blur-sm px-4 animate-in fade-in duration-300">
          <div className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl animate-in zoom-in-95 duration-200">
            <h3 className="text-zinc-100 font-bold mb-3 flex items-center gap-2 text-lg">
              <FiTrash2 className="text-red-500" />
              Dosyayı Sil?
            </h3>
            <p className="text-zinc-400 text-sm mb-6 leading-relaxed">
              <span className="text-zinc-100 font-mono font-bold bg-zinc-800 px-1 rounded">{fileToDelete}</span> kalıcı olarak silinecek. Bu işlem geri alınamaz. Emin misiniz?
            </p>
            <div className="flex gap-3">
              <button onClick={() => setFileToDelete(null)} className="flex-1 bg-zinc-900 border border-zinc-800 hover:bg-zinc-800 text-zinc-300 rounded-xl py-2.5 text-sm font-semibold transition-all">
                İPTAL
              </button>
              <button onClick={handleDeleteFile} className="flex-1 bg-red-500 hover:bg-red-600 text-white rounded-xl py-2.5 text-sm font-semibold shadow-md shadow-red-500/20 transition-all">
                SİL
              </button>
            </div>
          </div>
        </div>
      )}

      {isSettingsOpen && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-zinc-950/40 backdrop-blur-sm px-4">
          <div className="w-full max-w-sm max-h-[85vh] overflow-y-auto bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl custom-scrollbar">
            <div className="flex justify-between items-center mb-5">
              <h3 className="text-zinc-100 font-bold tracking-wide flex items-center gap-2 text-lg">
                <FiSettings className="text-zinc-500" />
                Entegrasyonlar
              </h3>
              <button onClick={() => setIsSettingsOpen(false)} className="text-zinc-500 hover:text-zinc-300 transition cursor-pointer p-1 hover:bg-zinc-800 rounded-md">
                <FiX className="w-5 h-5"/>
              </button>
            </div>
            
            <div className="space-y-4">
               <div>
                  <label className="block text-[11px] text-zinc-500 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE USERNAME</label>
                  <input type="text" value={kaggleUsername} onChange={e => setKaggleUsername(e.target.value)} className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-100 outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500/50 transition-all shadow-sm" placeholder="Kaggle kullanıcı adınız" />
               </div>
                <div>
                  <label className="block text-[11px] text-zinc-500 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE API KEY</label>
                  <input type="password" value={kaggleKey} onChange={e => setKaggleKey(e.target.value)} className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-100 outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500/50 transition-all shadow-sm" placeholder={isKaggleConfigured ? "•••••••••••• (Kayıtlı)" : "Kaggle API Key"} />
               </div>
               <div className="pt-4 border-t border-zinc-800">
                  <label className="block text-[11px] text-zinc-400 mb-1.5 tracking-wider font-semibold uppercase flex items-center gap-2">
                    <span className="w-1.5 h-1.5 bg-zinc-500 rounded-full"></span>
                    Hugging Face Access Token
                  </label>
                  <input 
                    type="password" 
                    value={hfTokenInput} 
                    onChange={e => setHfTokenInput(e.target.value)} 
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-100 outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500/50 transition-all shadow-sm" 
                    placeholder="hf_..." 
                  />
                  <p className="text-[10px] text-zinc-500 mt-2 leading-relaxed">
                    Model Hub'a aktarım yapmak için <b className="text-zinc-300">Write</b> yetkili bir token gereklidir.
                  </p>
               </div>
               <button disabled={isSavingSettings} onClick={handleSaveSettings} className="w-full mt-2 bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-100 rounded-xl py-3 text-sm font-bold transition-colors disabled:opacity-50 shadow-md">
                  {isSavingSettings ? 'Kaydediliyor...' : 'AYARLARI KAYDET'}
               </button>
            </div>
          </div>
        </div>
      )}

      {/* Snapshot Naming Modal */}
      {isSnapModalOpen && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-zinc-950/40 backdrop-blur-sm px-4 animate-in fade-in duration-300">
          <div className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-6 shadow-2xl animate-in zoom-in-95 duration-200">
            <h3 className="text-zinc-100 font-bold mb-2 flex items-center gap-2 text-lg">
              <FiArchive className="text-zinc-300" /> Yedek İsmi
            </h3>
            <p className="text-zinc-500 text-xs mb-5">Bu yedeği daha sonra hatırlamanıza yardımcı olacak kısa bir isim verin.</p>
            
            <input 
              type="text" 
              autoFocus
              value={snapName}
              onChange={(e) => setSnapName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleTakeSnapshot()}
              placeholder="Örn: Veri Temizliği Tamamlandı" 
              className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-3 text-sm text-zinc-100 outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500/50 transition-all mb-6 shadow-sm"
            />

            <div className="flex gap-3">
              <button 
                onClick={() => { setIsSnapModalOpen(false); setSnapName(''); }} 
                className="flex-1 bg-zinc-900 border border-zinc-800 hover:bg-zinc-800 text-zinc-300 rounded-xl py-2.5 text-sm font-semibold transition-all"
              >
                İPTAL
              </button>
              <button 
                onClick={handleTakeSnapshot} 
                className="flex-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-100 rounded-xl py-2.5 text-sm font-semibold shadow-md shadow-black/20 transition-all"
              >
                YEDEK AL
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Synthetic Data Factory Modal */}
      <SyntheticDataModal 
        workspaceId={activeWorkspace?.id || workspaceId}
        isOpen={isSyntheticModalOpen}
        onClose={() => setIsSyntheticModalOpen(false)}
        onSuccess={fetchFiles}
      />
    </div>
      {/* Global Tool Modals Overlay */}
      {isKaggleOpen && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-zinc-950/60 backdrop-blur-md px-4 animate-in fade-in duration-300">
           <div className="w-full max-w-2xl bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
              <div className="p-5 border-b border-zinc-800 flex justify-between items-center bg-zinc-900/50">
                 <h3 className="text-zinc-100 font-bold flex items-center gap-2">
                   <FiSearch className="text-zinc-300" />
                   Kaggle Dataset Keşfet
                 </h3>
                 <button onClick={() => setKaggleOpen(false)} className="p-1 hover:bg-zinc-800 rounded-lg text-zinc-500 hover:text-zinc-200 transition-colors"><FiX className="w-5 h-5" /></button>
              </div>
              <div className="p-6 h-[500px] flex flex-col">
                 <form onSubmit={handleKaggleSearch} className="relative mb-6">
                    <input 
                      type="text" 
                      value={kaggleSearch}
                      onChange={(e) => setKaggleSearch(e.target.value)}
                      placeholder="Veri kümesi ara (örn: Titanic, Netflix, Stocks)..." 
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl pl-10 pr-4 py-3 text-sm text-zinc-100 focus:ring-2 focus:ring-zinc-500/20 outline-none transition-all shadow-inner"
                    />
                    <FiSearch className="absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-500 w-4 h-4" />
                    <button type="submit" className="absolute right-2 top-1.5 bottom-1.5 px-4 bg-zinc-800 hover:bg-zinc-700 text-zinc-100 rounded-lg text-xs font-bold transition-colors">ARA</button>
                 </form>

                 <div className="flex-1 overflow-y-auto space-y-3 custom-scrollbar pr-2">
                    {isSearchingKaggle ? (
                       <div className="h-full flex flex-col items-center justify-center gap-4 text-zinc-500">
                          <div className="w-8 h-8 border-4 border-current border-t-transparent rounded-full animate-spin" />
                          <span className="text-xs font-bold tracking-[0.2em] uppercase text-zinc-500">Kaggle Aranıyor...</span>
                       </div>
                    ) : kaggleResults.length > 0 ? (
                       kaggleResults.map((kr: KaggleResult) => (
                          <div key={kr.ref} className="group bg-zinc-950/40 border border-zinc-800/50 hover:border-emerald-500/50 rounded-xl p-4 transition-all hover:bg-zinc-950/60 shadow-sm">
                             <div className="flex justify-between items-center gap-4">
                                <div className="flex-1 min-w-0">
                                   <h4 className="text-sm font-bold text-zinc-200 group-hover:text-emerald-400 transition-colors truncate mb-1">{kr.title || kr.ref}</h4>
                                   <div className="flex items-center gap-2 text-[10px] text-zinc-500 font-medium">
                                      <span>{kr.size || 'N/A'}</span>
                                      <span className="w-1 h-1 bg-zinc-700 rounded-full" />
                                      <span className="truncate">@{kr.ref}</span>
                                   </div>
                                </div>
                                <button 
                                  disabled={downloadingDataset === kr.ref}
                                  onClick={() => handleKaggleDownload(kr.ref)}
                                  className="bg-zinc-900 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 px-4 py-2 rounded-lg text-xs font-black tracking-widest transition-all disabled:opacity-50 shrink-0"
                                >
                                  {downloadingDataset === kr.ref ? <FiLoader className="w-4 h-4 animate-spin" /> : 'İNDİR'}
                                </button>
                             </div>
                          </div>
                       ))
                    ) : (
                       <div className="h-full flex flex-col items-center justify-center text-center p-12">
                          <div className="w-16 h-16 bg-zinc-950 rounded-2xl flex items-center justify-center mb-4 border border-zinc-800 shadow-inner">
                             <FiSearch className="w-7 h-7 text-zinc-700" />
                          </div>
                          <p className="text-sm text-zinc-400 font-bold mb-1">Dataset Dünyasına Bağlanın</p>
                          <p className="text-xs text-zinc-500 max-w-xs leading-relaxed">Kaggle üzerindeki milyonlarca veri kümesini doğrudan Currere sandbox'ınıza aktarın.</p>
                       </div>
                    )}
                 </div>
              </div>
           </div>
        </div>
      )}

      {isHistoryOpen && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-zinc-950/60 backdrop-blur-md px-4 animate-in fade-in duration-300">
           <div className="w-full max-w-xl bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
              <div className="p-5 border-b border-zinc-800 flex justify-between items-center bg-zinc-900/50">
                 <h3 className="text-zinc-100 font-bold flex items-center gap-2">
                   <FiArchive className="text-zinc-300" />
                   Çalışma Alanı Yedekleri
                 </h3>
                 <div className="flex items-center gap-2">
                    <button onClick={() => setIsSnapModalOpen(true)} className="px-3 py-1.5 bg-zinc-800/50 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-[10px] font-black tracking-widest transition-all">YENİ YEDEK</button>
                    <button onClick={() => setHistoryOpen(false)} className="p-1 hover:bg-zinc-800 rounded-lg text-zinc-500 hover:text-zinc-200 transition-colors"><FiX className="w-5 h-5" /></button>
                 </div>
              </div>
              <div className="p-6 h-[400px] overflow-y-auto custom-scrollbar space-y-3">
                 {snapshots.length === 0 ? (
                    <div className="h-full flex flex-col items-center justify-center text-center p-12">
                       <FiClock className="w-12 h-12 text-zinc-800 mb-4" />
                       <p className="text-sm text-zinc-500 font-medium italic">Henüz hiçbir yedek alınmamış.</p>
                    </div>
                 ) : (
                    snapshots.map((snap: any) => (
                       <div key={snap.id} className="bg-zinc-950/40 border border-zinc-800/50 rounded-xl p-4 transition-all hover:bg-zinc-950/60 group">
                          <div className="flex justify-between items-center">
                             <div className="flex flex-col min-w-0 pr-4">
                                <div className="flex items-center gap-2 mb-1.5">
                                   <div className="w-1.5 h-1.5 rounded-full bg-zinc-500 shadow-[0_0_8px_rgba(161,161,170,0.5)]"></div>
                                   <span className="text-[10px] uppercase font-black text-zinc-500 tracking-widest">
                                     {new Date(snap.createdAt).toLocaleString('tr-TR')}
                                   </span>
                                </div>
                                <span className="text-sm font-bold text-zinc-200 truncate group-hover:text-emerald-400 transition-colors">
                                   {snap.label || snap.description || 'İsimsiz Yedek'}
                                </span>
                             </div>
                             <div className="flex items-center gap-2 shrink-0">
                                <button 
                                  onClick={() => handleRestoreSnapshot(snap.id)}
                                  className="px-3 py-1.5 bg-zinc-900 hover:bg-zinc-700 text-zinc-300 rounded-lg text-[10px] font-black transition-all"
                                >
                                  DÖN
                                </button>
                                <button 
                                  onClick={() => handleDeleteSnapshot(snap.id)}
                                  className="p-2 text-zinc-600 hover:text-red-500 hover:bg-red-500/10 rounded-lg transition-all"
                                >
                                  <FiTrash2 className="w-4 h-4" />
                                </button>
                             </div>
                          </div>
                       </div>
                    ))
                 )}
              </div>
           </div>
        </div>
      )}

      {isSyntheticOpen && (
        <SyntheticDataModal 
          isOpen={isSyntheticOpen} 
          onClose={() => setSyntheticOpen(false)} 
          workspaceId={workspaceId} 
        />
      )}
    </>
  );
}
