import { useEffect, useState, useRef } from 'react';
import api from '@/services/api';
import axios from 'axios';
import { FiUpload, FiFile, FiImage, FiPlus } from 'react-icons/fi';
import { DiPython } from 'react-icons/di';
import { BsFiletypeCsv, BsFiletypeJson, BsFiletypeSql } from 'react-icons/bs';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

interface WorkspaceFile {
  id: number;
  fileName: string;
  uploadedAt: string;
  expiresAt: string;
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
  
  const fileInputRef = useRef<HTMLInputElement>(null);

  const fetchFiles = async () => {
    try {
      setLoading(true);
      const response = await api.get(`/workspace/${workspaceId}/file`);
      setFiles(response.data);
    } catch (error) {
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
      
      {/* Header */}
      <div className="flex flex-col border-b border-[#2d2d2d] shrink-0 text-zinc-300">
        <div className="h-10 flex items-center justify-between px-3 md:px-4">
          <span className="text-xs font-semibold tracking-wider hidden md:block">DOSYALAR</span>
          <div className="flex gap-3">
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

        {/* New File Input Area */}
        {isCreatingFile && (
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

      {/* File List */}
      <div className="flex-1 overflow-y-auto py-2">
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
      </div>
    </div>
  );
}
