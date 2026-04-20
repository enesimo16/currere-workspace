import { useEffect, useState, useRef } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { FiUpload, FiFile, FiImage, FiPlus, FiSettings, FiSearch, FiDownload, FiX } from 'react-icons/fi';
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

export default function FileExplorer({ workspaceId }: FileExplorerProps) {
  const { activeFile, setActiveFile } = useWorkspaceStore();
  const [files, setFiles] = useState<WorkspaceFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isCreatingFile, setIsCreatingFile] = useState(false);
  const [newFileName, setNewFileName] = useState('');
  
  const [activeTab, setActiveTab] = useState<'files' | 'kaggle'>('files');
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  
  // Kaggle States
  const [kaggleUsername, setKaggleUsername] = useState('');
  const [kaggleKey, setKaggleKey] = useState('');
  const [isSavingSettings, setIsSavingSettings] = useState(false);
  const [kaggleSearch, setKaggleSearch] = useState('');
  const [kaggleResults, setKaggleResults] = useState<KaggleResult[]>([]);
  const [isSearchingKaggle, setIsSearchingKaggle] = useState(false);
  const [downloadingDataset, setDownloadingDataset] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const fetchFiles = async () => {
    try {
      setLoading(true);
      const response = await api.get(`/workspace/${workspaceId}/file`);
      setFiles(response.data);
    } catch (error: unknown) {
      console.error('Dosyalar getirilirken hata oluştu:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (workspaceId) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      fetchFiles();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [workspaceId]);

  const handleFileUploadClick = () => {
    if (!isUploading) {
      fileInputRef.current?.click();
    }
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
    } catch (error: unknown) {
      console.error('Dosya oluşturma hatası:', error);
      let errorMsg = 'Bilinmeyen bir hata oluştu.';
      if (axios.isAxiosError(error)) {
        errorMsg = error.response?.data?.error || error.response?.data?.message || error.message;
      } else if (error instanceof Error) {
        errorMsg = error.message;
      }
      alert(`Dosya oluşturulamadı: ${errorMsg}`);
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
      formData.append('file', file); // Backend expects "file"

      await api.post(`/workspace/${workspaceId}/file/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      
      // Clear input so same file can be uploaded again if needed
      if (fileInputRef.current) fileInputRef.current.value = '';
      
      // Refresh the file list
      await fetchFiles();
    } catch (err: unknown) {
      console.error('Dosya yükleme hatası:', err);
      let errorMsg = 'Bilinmeyen bir hata oluştu.';
      if (axios.isAxiosError(err)) {
        errorMsg = err.response?.data?.error || err.response?.data?.message || err.message;
      } else if (err instanceof Error) {
        errorMsg = err.message;
      }
      alert(`Dosya yüklenemedi: ${errorMsg}`);
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
      await api.post('/integration/kaggle', {
        username: kaggleUsername.trim(),
        apiKey: kaggleKey.trim()
      });
      alert('Ayarlar başarıyla kaydedildi.');
      setIsSettingsOpen(false);
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      alert(`Ayarlar kaydedilemedi: ${errorMsg}`);
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
    } catch (err: unknown) {
      let errorMsg = 'Aranırken bir hata oluştu.';
      if (axios.isAxiosError(err)) {
        errorMsg = err.response?.data?.error || err.message;
      } else if (err instanceof Error) {
        errorMsg = err.message;
      }
      alert(`Kaggle araması başarısız: ${errorMsg}`);
    } finally {
      setIsSearchingKaggle(false);
    }
  };

  const handleKaggleDownload = async (datasetRef: string) => {
    try {
      setDownloadingDataset(datasetRef);
      await api.post(`/workspace/${workspaceId}/kaggle/download`, { datasetRef });
      alert('Veri seti başarıyla çalışma alanına eklendi.');
      fetchFiles(); // Refresh file list
      setActiveTab('files'); // Switch to files view to see it
    } catch (err: unknown) {
      let errorMsg = 'İndirilirken bir hata oluştu.';
      if (axios.isAxiosError(err)) {
        errorMsg = err.response?.data?.error || err.message;
      } else if (err instanceof Error) {
        errorMsg = err.message;
      }
      alert(`İndirme başarısız: ${errorMsg}`);
    } finally {
      setDownloadingDataset(null);
    }
  };

  const getFileIcon = (fileName: string) => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'py':
      case 'ipynb':
        return <DiPython className="text-blue-400 w-5 h-5" />;
      case 'csv':
      case 'xlsx':
        return <BsFiletypeCsv className="text-emerald-400 w-4 h-4" />;
      case 'json':
        return <BsFiletypeJson className="text-yellow-400 w-4 h-4" />;
      case 'sql':
        return <BsFiletypeSql className="text-orange-400 w-4 h-4" />;
      case 'png':
      case 'jpg':
      case 'jpeg':
        return <FiImage className="text-purple-400 w-4 h-4" />;
      default:
        return <FiFile className="text-zinc-400 w-4 h-4" />;
    }
  };

  return (
    <div className="w-16 md:w-64 h-full bg-[#1e1e1e] border-r border-[#2d2d2d] flex flex-col shrink-0 transition-all duration-300">
      {/* Hidden File Input */}
      <input 
        type="file" 
        className="hidden" 
        ref={fileInputRef} 
        onChange={handleFileChange}
        accept=".csv,.xlsx,.json,.txt,.py,.ipynb"
      />
      
      {/* Header Tabs */}
      <div className="flex flex-col border-b border-[#2d2d2d] shrink-0 text-zinc-300">
        <div className="flex items-center gap-4 px-4 pt-3 pb-2 text-[11px] font-bold tracking-widest hidden md:flex border-b border-[#2d2d2d]/50">
           <button onClick={() => setActiveTab('files')} className={`transition-colors ${activeTab === 'files' ? 'text-emerald-400' : 'text-zinc-500 hover:text-zinc-300'}`}>DOSYALAR</button>
           <button onClick={() => setActiveTab('kaggle')} className={`transition-colors ${activeTab === 'kaggle' ? 'text-emerald-400' : 'text-zinc-500 hover:text-zinc-300'}`}>KAGGLE</button>
        </div>

        {activeTab === 'files' && (
          <div className="h-10 flex items-center justify-between px-3 md:px-4">
            <span className="text-xs font-semibold tracking-wider hidden md:block opacity-0">.</span>
            <div className="flex gap-3 w-full justify-end">
              <button 
                title="Yeni Dosya"
                onClick={() => setIsCreatingFile(!isCreatingFile)}
                className="p-1.5 rounded-md hover:bg-[#2d2d2d] text-zinc-400 hover:text-zinc-100 transition-colors"
              >
                <FiPlus className="w-4 h-4" />
              </button>
              <button 
                title="Dosya Yükle"
                onClick={handleFileUploadClick}
                disabled={isUploading}
                className={`p-1.5 rounded-md transition-colors ${
                  isUploading 
                    ? 'opacity-50 cursor-not-allowed text-emerald-400' 
                    : 'hover:bg-[#2d2d2d] text-zinc-400 hover:text-zinc-100'
                }`}
              >
                <FiUpload className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}

        {/* New File Input Area */}
        {activeTab === 'files' && isCreatingFile && (
          <div className="px-3 pb-2 flex items-center gap-2">
            <input 
              type="text" 
              autoFocus
              value={newFileName}
              onChange={(e) => setNewFileName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleFileCreate()}
              placeholder="utils.py"
              className="w-full bg-[#111111] text-zinc-200 text-xs rounded-md px-2 py-1.5 outline-none border border-gray-700/50 focus:border-emerald-500/50"
            />
          </div>
        )}
      </div>

      {/* Main Content Area */}
      <div className="flex-1 overflow-y-auto py-2">
        {activeTab === 'kaggle' ? (
          <div className="px-3">
            <div className="relative flex items-center mb-4">
              <input 
                type="text" 
                value={kaggleSearch}
                onChange={(e) => setKaggleSearch(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleKaggleSearch()}
                placeholder="Titanic, MNIST..."
                className="w-full bg-[#111111] text-zinc-200 text-xs rounded-lg pl-3 pr-8 py-2 outline-none border border-gray-700/50 focus:border-emerald-500/50 transition-colors"
              />
              <button 
                onClick={handleKaggleSearch}
                disabled={isSearchingKaggle}
                className="absolute right-2 text-zinc-400 hover:text-emerald-400 transition-colors disabled:opacity-50"
              >
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
                  <button 
                    onClick={() => handleKaggleDownload(kr.ref)}
                    disabled={downloadingDataset === kr.ref}
                    className="shrink-0 p-1.5 rounded bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 disabled:opacity-50 transition-colors"
                    title="İndir ve Ekle"
                  >
                    {downloadingDataset === kr.ref ? (
                      <div className="w-3.5 h-3.5 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
                    ) : (
                      <FiDownload className="w-3.5 h-3.5" />
                    )}
                  </button>
                </div>
              ))}
              {kaggleResults.length === 0 && !isSearchingKaggle && (
                <p className="text-[10px] text-zinc-500 italic text-center mt-4">Kaggle&apos;da veri seti arayın.</p>
              )}
            </div>
          </div>
        ) : (
          <>
            <div className="px-3 py-1 mb-1 text-[10px] font-semibold text-zinc-500 tracking-widest hidden md:block flex items-center justify-between">
              <span>ÇALIŞMA ALANI DOSYALARI</span>
              {isUploading && (
                <span className="ml-2 inline-block w-3 h-3 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></span>
              )}
            </div>

            {loading ? (
              <div className="px-4 py-3 text-xs text-zinc-500 hidden md:block animate-pulse">Yükleniyor...</div>
            ) : (
              <div className="px-2 flex flex-col gap-0.5">
                {/* main.py is strictly integrated into the top of the file list */}
                <div 
                  title="main.py"
                  onClick={() => setActiveFile({ name: 'main.py', type: 'code' })}
                  className={`group flex items-center gap-2.5 px-2.5 py-1.5 rounded-md cursor-pointer text-sm transition-colors border ${
                    activeFile.name === 'main.py' 
                      ? 'bg-[#2d2d2d] text-zinc-200 border-[#3d3d3d]' 
                      : 'text-zinc-400 border-transparent hover:bg-[#2d2d2d]/50 hover:text-zinc-200'
                  }`}
                >
                  <div className="shrink-0"><DiPython className="text-emerald-500 w-5 h-5 bg-emerald-500/10 rounded-sm" /></div>
                  <span className="truncate hidden md:block font-medium">main.py</span>
                </div>

                {files.length === 0 && (
                  <div className="px-2.5 py-2 text-xs text-zinc-500 hidden md:block italic">
                    Başka dosya yok.
                  </div>
                )}

                {/* Other files */}
                {files.map((file) => (
                  <div 
                    key={file.id}
                    title={file.fileName}
                    onClick={() => setActiveFile({ name: file.fileName, type: 'file' })}
                    className={`group flex items-center gap-2.5 px-2.5 py-1.5 rounded-md cursor-pointer text-sm transition-colors border ${
                      activeFile.name === file.fileName
                        ? 'bg-[#2d2d2d] text-zinc-200 border-[#3d3d3d]'
                        : 'text-zinc-400 border-transparent hover:bg-[#2d2d2d]/50 hover:text-zinc-200'
                    }`}
                  >
                    <div className="shrink-0">{getFileIcon(file.fileName)}</div>
                    <span className="truncate hidden md:block">{file.fileName}</span>
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>
      {/* Footer Settings */}
      <div className="mt-auto p-3 border-t border-[#2d2d2d] flex justify-center md:justify-start">
        <button onClick={() => setIsSettingsOpen(true)} className="flex items-center gap-2 text-zinc-400 hover:text-zinc-100 transition-colors w-full p-1.5 rounded hover:bg-[#2d2d2d]/50">
          <FiSettings className="w-4 h-4 shrink-0" />
          <span className="hidden md:block text-xs font-semibold tracking-wider">AYARLAR</span>
        </button>
      </div>

      {/* Settings Modal */}
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
