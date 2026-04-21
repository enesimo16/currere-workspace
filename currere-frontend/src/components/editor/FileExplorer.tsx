import { useEffect, useState, useRef, useCallback } from 'react';
import api from '@/services/api';
import { FiUpload, FiFile, FiImage, FiPlus, FiSettings, FiSearch, FiDownload, FiX, FiEdit, FiTrash2, FiList, FiGrid, FiChevronDown } from 'react-icons/fi';
import { DiPython } from 'react-icons/di';
import { BsFiletypeCsv, BsFiletypeJson, BsFiletypeSql } from 'react-icons/bs';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import toast from 'react-hot-toast';

interface WorkspaceFile {
  id: number;
  fileName: string;
  uploadedAt: string;
  expiresAt: string;
}

interface KaggleResult {
  ref: string;
  title: string;
}

interface FileExplorerProps {
  workspaceId: string | number;
}

const FileItem = ({ 
  file, 
  isMain = false, 
  level = 0, 
  isLast = false,
  activeFileName,
  setActiveFile,
  viewMode,
  renamingFileId,
  setRenamingFileId,
  newName,
  setNewName,
  handleRename,
  handleDeleteRequest,
  handleDownloadFile,
  getFileIcon
}: { 
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  file: any; 
  isMain?: boolean; 
  level?: number;
  isLast?: boolean;
  activeFileName: string;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  setActiveFile: (f: any) => void;
  viewMode: 'list' | 'tree';
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

  return (
    <div className="relative">
      {viewMode === 'tree' && level > 0 && (
        <>
          <div 
            className="absolute left-[-14px] top-0 bottom-0 border-l border-gray-800" 
            style={{ height: isLast ? '16px' : '100%' }}
          />
          <div className="absolute left-[-14px] top-[16px] w-[14px] border-b border-gray-800" />
        </>
      )}

      <div 
        title={fileName}
        onClick={() => {
           if (!isRenaming) {
             setActiveFile({ 
               id: isMain ? null : file.id,
               name: fileName, 
               type: fileName.endsWith('.csv') ? 'file' : 'code' 
             });
           }
        }}
        style={{ paddingLeft: viewMode === 'tree' ? `${level * 16 + 8}px` : '10px' }}
        className={`group flex items-center gap-2.5 py-1.5 rounded-md cursor-pointer text-sm transition-all border ${
          isActive
            ? 'bg-[#2d2d2d] text-zinc-200 border-emerald-500/20 shadow-sm'
            : 'text-zinc-400 border-transparent hover:bg-[#2d2d2d]/30 hover:text-zinc-300'
        }`}
      >
        <div className="shrink-0">
          {isMain ? <DiPython className="text-emerald-500 w-5 h-5 bg-emerald-500/10 rounded-sm" /> : getFileIcon(fileName)}
        </div>
        
        {isRenaming ? (
          <input
            autoFocus
            className="flex-1 min-w-0 bg-[#111111] text-zinc-200 text-xs px-1.5 py-0.5 rounded border border-emerald-500/50 outline-none"
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
          <span className={`truncate hidden md:block flex-1 ${isActive ? 'font-medium' : 'font-normal'}`}>
            {fileName}
          </span>
        )}

        <div className="hidden group-hover:flex items-center gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity pr-1">
          {!isMain && (
            <button 
              onClick={(e) => { e.stopPropagation(); setRenamingFileId(file.id); setNewName(file.fileName); }}
              className="p-1 hover:text-emerald-400 transition-colors"
              title="Yeniden Adlandır"
            >
              <FiEdit className="w-3 h-3" />
            </button>
          )}
          <button 
            onClick={(e) => { e.stopPropagation(); handleDownloadFile(fileName); }}
            className="p-1 hover:text-emerald-400 transition-colors"
            title="İndir"
          >
            <FiDownload className="w-3 h-3" />
          </button>
          {!isMain && (
            <button 
              onClick={(e) => { e.stopPropagation(); handleDeleteRequest(file.fileName); }}
              className="p-1 hover:text-red-400 transition-colors"
              title="Sil"
            >
              <FiTrash2 className="w-3 h-3" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default function FileExplorer({ workspaceId }: FileExplorerProps) {
  const { activeFile, setActiveFile, viewMode, setViewMode } = useWorkspaceStore();
  const [files, setFiles] = useState<WorkspaceFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isCreatingFile, setIsCreatingFile] = useState(false);
  const [isExtMenuOpen, setIsExtMenuOpen] = useState(false);
  const [newFileName, setNewFileName] = useState('');
  const [newFileExt, setNewFileExt] = useState('.py');
  const [activeTab, setActiveTab] = useState<'files' | 'kaggle'>('files');
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [kaggleUsername, setKaggleUsername] = useState('');
  const [kaggleKey, setKaggleKey] = useState('');
  const [isSavingSettings, setIsSavingSettings] = useState(false);
  const [kaggleSearch, setKaggleSearch] = useState('');
  const [kaggleResults, setKaggleResults] = useState<KaggleResult[]>([]);
  const [isSearchingKaggle, setIsSearchingKaggle] = useState(false);
  const [downloadingDataset, setDownloadingDataset] = useState<string | null>(null);
  const [renamingFileId, setRenamingFileId] = useState<number | null>(null);
  const [newName, setNewName] = useState('');
  const [fileToDelete, setFileToDelete] = useState<string | null>(null);

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

  useEffect(() => {
    if (workspaceId) {
       const timer = setTimeout(() => {
         fetchFiles();
       }, 0);
       return () => clearTimeout(timer);
    }
  }, [workspaceId, fetchFiles]);

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
      setActiveFile({ 
        id: res.data.fileId,
        name: fullName, 
        type: fullName.endsWith('.csv') ? 'file' : 'code' 
      });
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
    if (!kaggleUsername.trim() || !kaggleKey.trim()) {
      toast.error('Eksik bilgi girdiniz.');
      return;
    }
    try {
      setIsSavingSettings(true);
      await api.post('/integration/kaggle', { username: kaggleUsername.trim(), apiKey: kaggleKey.trim() });
      toast.success('Entegrasyon ayarları kaydedildi.');
      setIsSettingsOpen(false);
    } catch {
      toast.error('Ayarlar kaydedilemedi.');
    } finally {
      setIsSavingSettings(false);
    }
  };

  const handleKaggleSearch = async () => {
    if (!kaggleSearch.trim()) return;
    try {
      setIsSearchingKaggle(true);
      const res = await api.get(`/workspace/${workspaceId}/kaggle/search?query=${encodeURIComponent(kaggleSearch.trim())}`);
      setKaggleResults(res.data);
    } catch {
      toast.error('Kaggle araması başarısız.');
    } finally {
      setIsSearchingKaggle(false);
    }
  };

  const handleKaggleDownload = async (datasetRef: string) => {
    const toastId = toast.loading('Veri seti indiriliyor...');
    try {
      setDownloadingDataset(datasetRef);
      await api.post(`/workspace/${workspaceId}/kaggle/download`, { datasetRef });
      toast.success('Veri seti başarıyla eklendi.', { id: toastId });
      fetchFiles();
      setActiveTab('files');
    } catch {
      toast.error('İndirme başarısız.', { id: toastId });
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

  const handleDownloadFile = async (fileName: string) => {
    try {
      const response = await api.get(`/workspace/${workspaceId}/file/${fileName}/raw`);
      const content = response.data.content;
      const blob = new Blob([content], { type: 'text/plain' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch {
      toast.error('Dosya indirilemedi.');
    }
  };

  const getFileIcon = (fileName: string) => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'py': return <DiPython className="text-blue-400 w-5 h-5" />;
      case 'ipynb': return <FiFile className="text-orange-400 w-4 h-4" />;
      case 'csv': case 'xlsx': return <BsFiletypeCsv className="text-emerald-400 w-4 h-4" />;
      case 'json': return <BsFiletypeJson className="text-yellow-400 w-4 h-4" />;
      case 'sql': return <BsFiletypeSql className="text-orange-400 w-4 h-4" />;
      case 'png': case 'jpg': case 'jpeg': return <FiImage className="text-purple-400 w-4 h-4" />;
      default: return <FiFile className="text-zinc-400 w-4 h-4" />;
    }
  };

  return (
    <div className="w-16 md:w-64 h-full bg-[#1e1e1e] border-r border-[#2d2d2d] flex flex-col shrink-0 transition-all duration-300">
      <input type="file" className="hidden" ref={fileInputRef} onChange={handleFileChange} accept=".csv,.xlsx,.json,.txt,.py,.ipynb" />
      
      <div className="flex flex-col border-b border-[#2d2d2d] shrink-0 text-zinc-300">
        <div className="flex items-center gap-4 px-4 pt-3 pb-2 text-[11px] font-bold tracking-widest hidden md:flex border-b border-[#2d2d2d]/50">
           <button onClick={() => setActiveTab('files')} className={`transition-colors ${activeTab === 'files' ? 'text-emerald-400' : 'text-zinc-500 hover:text-zinc-300'}`}>DOSYALAR</button>
           <button onClick={() => setActiveTab('kaggle')} className={`transition-colors ${activeTab === 'kaggle' ? 'text-emerald-400' : 'text-zinc-500 hover:text-zinc-300'}`}>KAGGLE</button>
        </div>

        {activeTab === 'files' && (
          <div className="h-10 flex items-center justify-between px-3 md:px-4">
            <span className="text-xs font-semibold tracking-wider hidden md:block opacity-0">.</span>
            <div className="flex gap-1.5 w-full justify-end items-center">
              <div className="flex bg-[#111111] rounded-md p-0.5 border border-gray-800 mr-2 shadow-inner">
                <button onClick={() => setViewMode('list')} className={`p-1 rounded-sm transition-all ${viewMode === 'list' ? 'bg-[#2d2d2d] text-emerald-400 shadow-sm' : 'text-zinc-500 hover:text-zinc-300'}`} title="Liste Görünümü"><FiList className="w-3.5 h-3.5" /></button>
                <button onClick={() => setViewMode('tree')} className={`p-1 rounded-sm transition-all ${viewMode === 'tree' ? 'bg-[#2d2d2d] text-emerald-400 shadow-sm' : 'text-zinc-500 hover:text-zinc-300'}`} title="Ağaç Görünümü"><FiGrid className="w-3.5 h-3.5" /></button>
              </div>
              <button onClick={() => setIsCreatingFile(!isCreatingFile)} className="p-1.5 rounded-md hover:bg-[#2d2d2d] text-zinc-400 hover:text-zinc-100 transition-colors" title="Yeni Dosya"><FiPlus className="w-4 h-4" /></button>
              <button onClick={handleFileUploadClick} title="Dosya Yükle" disabled={isUploading} className={`p-1.5 rounded-md transition-colors ${isUploading ? 'opacity-50 cursor-not-allowed text-emerald-400' : 'hover:bg-[#2d2d2d] text-zinc-400 hover:text-zinc-100'}`}><FiUpload className="w-4 h-4" /></button>
            </div>
          </div>
        )}

        {activeTab === 'files' && isCreatingFile && (
          <div className="px-3 pb-2 flex flex-col gap-2">
            <div className="flex items-center gap-1 bg-[#111111] border border-gray-800 rounded-md px-1 py-0.5">
               <input type="text" autoFocus value={newFileName} onChange={(e) => setNewFileName(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleFileCreate()} placeholder="Dosya adı..." className="flex-1 bg-transparent text-zinc-200 text-xs px-2 py-1 outline-none font-mono" />
               <div className="relative">
                  <button 
                    onMouseDown={(e) => { e.stopPropagation(); setIsExtMenuOpen(!isExtMenuOpen); }}
                    className="flex items-center gap-1 text-[10px] bg-[#2d2d2d] hover:bg-[#3d3d3d] text-emerald-400 px-1.5 py-1 rounded transition-colors uppercase font-bold"
                  >
                    {newFileExt.replace('.', '')}
                    <FiChevronDown className={`w-3 h-3 transition-transform ${isExtMenuOpen ? 'rotate-180' : ''}`} />
                  </button>
                  {isExtMenuOpen && (
                    <div className="absolute right-0 top-full mt-1 bg-[#2d2d2d] border border-gray-700 rounded shadow-xl z-50 min-w-[100px] overflow-hidden animate-in fade-in slide-in-from-top-1 duration-200">
                      <button 
                        onMouseDown={() => { setNewFileExt('.py'); setIsExtMenuOpen(false); }}
                        className="w-full text-left px-3 py-2 text-[10px] hover:bg-emerald-500/10 hover:text-emerald-400 transition-colors border-b border-gray-700/50 font-bold"
                      >
                        PYTHON (.py)
                      </button>
                      <button 
                        onMouseDown={() => { setNewFileExt('.ipynb'); setIsExtMenuOpen(false); }}
                        className="w-full text-left px-3 py-2 text-[10px] hover:bg-emerald-500/10 hover:text-emerald-400 transition-colors font-bold"
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
        {activeTab === 'kaggle' ? (
          <div className="px-3">
            <div className="relative flex items-center mb-4">
              <input type="text" value={kaggleSearch} onChange={(e) => setKaggleSearch(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleKaggleSearch()} placeholder="Titanic, MNIST..." className="w-full bg-[#111111] text-zinc-200 text-xs rounded-lg pl-3 pr-8 py-2 outline-none border border-gray-700/50 focus:border-emerald-500/50 transition-colors" />
              <button onClick={handleKaggleSearch} disabled={isSearchingKaggle} className="absolute right-2 text-zinc-400 hover:text-emerald-400 transition-colors disabled:opacity-50">
                {isSearchingKaggle ? <div className="w-3.5 h-3.5 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div> : <FiSearch className="w-4 h-4" />}
              </button>
            </div>
            <div className="flex flex-col gap-2">
              {kaggleResults.map((kr: KaggleResult) => (
                <div key={kr.ref} className="bg-[#1a1a1a] border border-[#2d2d2d] rounded-lg p-2.5 flex justify-between items-start gap-2">
                  <div className="flex-1 min-w-0">
                    <p className="text-[11px] font-semibold text-zinc-200 truncate" title={kr.title}>{kr.title}</p>
                    <p className="text-[10px] text-zinc-500 truncate">@{kr.ref.split('/')[0]}</p>
                  </div>
                  <button onClick={() => handleKaggleDownload(kr.ref)} disabled={downloadingDataset === kr.ref} className="shrink-0 p-1.5 rounded bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors">
                    {downloadingDataset === kr.ref ? <div className="w-3.5 h-3.5 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div> : <FiDownload className="w-3.5 h-3.5" />}
                  </button>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <>
            <div className="px-3 py-1 mb-1 text-[10px] font-semibold text-zinc-500 tracking-widest hidden md:block flex items-center justify-between">
              <span>ÇALIŞMA ALANI DOSYALARI</span>
              {isUploading && <span className="ml-2 inline-block w-3 h-3 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></span>}
            </div>

            {loading ? (
              <div className="px-4 py-3 text-xs text-zinc-500 hidden md:block animate-pulse">Yükleniyor...</div>
            ) : (
              <div className={`px-2 flex flex-col ${viewMode === 'tree' ? 'gap-0 ml-2' : 'gap-0.5'}`}>
                <FileItem 
                  isMain={true} 
                  file={{}} 
                  level={0} 
                  activeFileName={activeFile.name}
                  setActiveFile={setActiveFile}
                  viewMode={viewMode}
                  renamingFileId={renamingFileId}
                  setRenamingFileId={setRenamingFileId}
                  newName={newName}
                  setNewName={setNewName}
                  handleRename={handleRename}
                  handleDeleteRequest={() => toast.error('Ana dosya silinemez.')}
                  handleDownloadFile={handleDownloadFile}
                  getFileIcon={getFileIcon}
                />

                {files.length === 0 && (
                  <div className="px-2.5 py-2 text-xs text-zinc-500 hidden md:block italic">
                    {viewMode === 'tree' ? (
                      <div className="relative pt-1">
                        <div className="absolute left-[-14px] top-0 bottom-0 border-l border-gray-800" style={{ height: '16px' }} />
                        <div className="absolute left-[-14px] top-[16px] w-[10px] border-b border-gray-800" />
                        <span className="ml-2 opacity-50">Dosya yok</span>
                      </div>
                    ) : 'Başka dosya yok.'}
                  </div>
                )}

                <div className={viewMode === 'tree' ? 'flex flex-col' : 'flex flex-col gap-0.5'}>
                  {files.map((file, index) => (
                    <FileItem 
                      key={file.id} 
                      file={file} 
                      level={viewMode === 'tree' ? 1 : 0} 
                      isLast={index === files.length - 1} 
                      activeFileName={activeFile.name}
                      setActiveFile={setActiveFile}
                      viewMode={viewMode}
                      renamingFileId={renamingFileId}
                      setRenamingFileId={setRenamingFileId}
                      newName={newName}
                      setNewName={setNewName}
                      handleRename={handleRename}
                      handleDeleteRequest={(name) => setFileToDelete(name)}
                      handleDownloadFile={handleDownloadFile}
                      getFileIcon={getFileIcon}
                    />
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <div className="mt-auto p-3 border-t border-[#2d2d2d] flex justify-center md:justify-start">
        <button onClick={() => setIsSettingsOpen(true)} className="flex items-center gap-2 text-zinc-400 hover:text-zinc-100 transition-colors w-full p-1.5 rounded hover:bg-[#2d2d2d]/50">
          <FiSettings className="w-4 h-4 shrink-0" />
          <span className="hidden md:block text-xs font-semibold tracking-wider uppercase">AYARLAR</span>
        </button>
      </div>

      {/* Delete Confirmation Modal */}
      {fileToDelete && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-black/70 backdrop-blur-md px-4 animate-in fade-in duration-300">
          <div className="w-full max-w-sm bg-[#1a1a1a] border border-red-500/20 rounded-2xl p-6 shadow-2xl animate-in zoom-in-95 duration-200">
            <h3 className="text-zinc-100 font-bold mb-2 flex items-center gap-2">
              <FiTrash2 className="text-red-500" />
              Dosyayı Sil?
            </h3>
            <p className="text-zinc-400 text-xs mb-6 leading-relaxed">
              <span className="text-zinc-200 font-mono font-bold">{fileToDelete}</span> kalıcı olarak silinecek. Bu işlem geri alınamaz. Emin misiniz?
            </p>
            <div className="flex gap-3">
              <button onClick={() => setFileToDelete(null)} className="flex-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 rounded-xl py-2.5 text-xs font-bold transition-all">
                İPTAL
              </button>
              <button onClick={handleDeleteFile} className="flex-1 bg-red-600 hover:bg-red-500 text-white rounded-xl py-2.5 text-xs font-bold shadow-lg shadow-red-600/20 transition-all">
                SİL
              </button>
            </div>
          </div>
        </div>
      )}

      {isSettingsOpen && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 backdrop-blur-sm px-4">
          <div className="w-full max-w-sm bg-[#1e1e1e]/95 border border-[#2d2d2d] rounded-2xl p-6 shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-zinc-100 font-semibold tracking-wide flex items-center gap-2">
                <FiSettings className="text-emerald-400" />
                Entegrasyonlar
              </h3>
              <button onClick={() => setIsSettingsOpen(false)} className="text-zinc-400 hover:text-white transition cursor-pointer">
                <FiX className="w-5 h-5"/>
              </button>
            </div>
            
            <div className="space-y-4">
               <div>
                  <label className="block text-xs text-zinc-400 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE USERNAME</label>
                  <input type="text" value={kaggleUsername} onChange={e => setKaggleUsername(e.target.value)} className="w-full bg-[#111111] border border-gray-700/50 rounded-lg px-3 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 transition-colors" placeholder="Kaggle kullanıcı adınız" />
               </div>
               <div>
                  <label className="block text-xs text-zinc-400 mb-1.5 tracking-wider font-semibold uppercase">KAGGLE API KEY</label>
                  <input type="password" value={kaggleKey} onChange={e => setKaggleKey(e.target.value)} className="w-full bg-[#111111] border border-gray-700/50 rounded-lg px-3 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 transition-colors" placeholder="Kaggle API Key" />
               </div>
               <button disabled={isSavingSettings} onClick={handleSaveSettings} className="w-full bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg py-2.5 text-sm font-medium transition-colors disabled:opacity-50 mt-2">
                  {isSavingSettings ? 'Kaydediliyor...' : 'Kaydet'}
               </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
