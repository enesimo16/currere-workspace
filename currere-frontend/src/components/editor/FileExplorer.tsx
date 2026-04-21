import { useEffect, useState, useRef, useCallback } from 'react';
import api from '@/services/api';
import { FiUpload, FiFile, FiImage, FiPlus, FiSettings, FiSearch, FiDownload, FiX, FiEdit, FiTrash2, FiList, FiGrid } from 'react-icons/fi';
import { DiPython } from 'react-icons/di';
import { BsFiletypeCsv, BsFiletypeJson, BsFiletypeSql } from 'react-icons/bs';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

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

// Sub-component for individual file item to handle both views
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
  handleDeleteFile,
  handleDownloadFile,
  getFileIcon
}: { 
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  file: any; 
  isMain?: boolean; 
  level?: number;
  isLast?: boolean;
  activeFileName: string;
  setActiveFile: (f: any) => void;
  viewMode: 'list' | 'tree';
  renamingFileId: number | null;
  setRenamingFileId: (id: number | null) => void;
  newName: string;
  setNewName: (name: string) => void;
  handleRename: (id: number, oldName: string) => void;
  handleDeleteFile: (name: string) => void;
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
               name: fileName, 
               type: fileName.endsWith('.csv') ? 'file' : 'code' 
             });
           }
        }}
        style={{ paddingLeft: viewMode === 'tree' ? `${level * 16 + 8}px` : '10px' }}
        className={`group flex items-center gap-2.5 py-1.5 rounded-md cursor-pointer text-sm transition-all border ${
          isActive
            ? 'bg-[#2d2d2d] text-zinc-200 border-[#3d3d3d] shadow-sm'
            : 'text-zinc-400 border-transparent hover:bg-[#2d2d2d]/40 hover:text-zinc-300'
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
              onClick={(e) => { e.stopPropagation(); handleDeleteFile(file.fileName); }}
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
  const [newFileName, setNewFileName] = useState('');
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
      // Use an IIFE or just call it, but silence the lint if it's about synchronous calls
      // The lint error "Avoid calling setState() directly within an effect" usually refers to 
      // setting state in the same component but fetchFiles is async.
      fetchFiles();
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
    try {
      setIsUploading(true);
      await api.post(`/workspace/${workspaceId}/file/create`, { fileName: newFileName.trim() });
      await fetchFiles();
      setActiveFile({ name: newFileName.trim(), type: 'code' });
      setNewFileName('');
      setIsCreatingFile(false);
    } catch {
      alert('Dosya oluşturulamadı.');
    } finally {
      setIsUploading(false);
    }
  };

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !workspaceId) return;
    try {
      setIsUploading(true);
      const formData = new FormData();
      formData.append('file', file);
      await api.post(`/workspace/${workspaceId}/file/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      if (fileInputRef.current) fileInputRef.current.value = '';
      await fetchFiles();
    } catch {
      alert('Dosya yüklenemedi.');
    } finally {
      setIsUploading(false);
    }
  };

  const handleSaveSettings = async () => {
    if (!kaggleUsername.trim() || !kaggleKey.trim()) {
      alert('Kaggle Username ve API Key zorunludur.');
      return;
    }
    try {
      setIsSavingSettings(true);
      await api.post('/integration/kaggle', { username: kaggleUsername.trim(), apiKey: kaggleKey.trim() });
      alert('Ayarlar başarıyla kaydedildi.');
      setIsSettingsOpen(false);
    } catch {
      alert('Ayarlar kaydedilemedi.');
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
      alert('Kaggle araması başarısız.');
    } finally {
      setIsSearchingKaggle(false);
    }
  };

  const handleKaggleDownload = async (datasetRef: string) => {
    try {
      setDownloadingDataset(datasetRef);
      await api.post(`/workspace/${workspaceId}/kaggle/download`, { datasetRef });
      alert('Veri seti başarıyla çalışma alanına eklendi.');
      fetchFiles();
      setActiveTab('files');
    } catch {
      alert('İndirme başarısız.');
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
        setActiveFile({ name: newName.trim(), type: newName.endsWith('.csv') ? 'file' : 'code' });
      }
      await fetchFiles();
      setRenamingFileId(null);
    } catch {
      alert('Dosya adı değiştirilemedi.');
    }
  };

  const handleDeleteFile = async (fileName: string) => {
    if (!window.confirm(`${fileName} dosyasını silmek istediğinize emin misiniz?`)) return;
    try {
      await api.delete(`/workspace/${workspaceId}/file/${fileName}`);
      if (activeFile.name === fileName) {
        setActiveFile({ name: 'main.py', type: 'code' });
      }
      await fetchFiles();
    } catch {
      alert('Dosya silinemedi.');
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
      alert('Dosya indirilemedi.');
    }
  };

  const getFileIcon = (fileName: string) => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'py': case 'ipynb': return <DiPython className="text-blue-400 w-5 h-5" />;
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
          <div className="px-3 pb-2 flex items-center gap-2">
            <input type="text" autoFocus value={newFileName} onChange={(e) => setNewFileName(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleFileCreate()} placeholder="utils.py" className="w-full bg-[#111111] text-zinc-200 text-xs rounded-md px-2 py-1.5 outline-none border border-gray-700/50 focus:border-emerald-500/50" />
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
                  handleDeleteFile={handleDeleteFile}
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
                      handleDeleteFile={handleDeleteFile}
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
          <span className="hidden md:block text-xs font-semibold tracking-wider">AYARLAR</span>
        </button>
      </div>

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
                  <label className="block text-xs text-zinc-400 mb-1.5 tracking-wider font-semibold">KAGGLE USERNAME</label>
                  <input type="text" value={kaggleUsername} onChange={e => setKaggleUsername(e.target.value)} className="w-full bg-[#111111] border border-gray-700/50 rounded-lg px-3 py-2.5 text-sm text-zinc-200 outline-none focus:border-emerald-500/50 transition-colors" placeholder="Kaggle kullanıcı adınız" />
               </div>
               <div>
                  <label className="block text-xs text-zinc-400 mb-1.5 tracking-wider font-semibold">KAGGLE API KEY</label>
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
