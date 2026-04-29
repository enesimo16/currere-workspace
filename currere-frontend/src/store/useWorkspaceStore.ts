import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface Workspace {
  id: string | number;
  name?: string;
  title?: string;
  createdAt?: string;
  currentState?: string;
  // include other properties if needed
}

export interface ActiveFile {
  id?: number | null;
  name: string;
  type: string;
}

export interface QuotedSnippet {
  id: string;
  type: 'code' | 'terminal';
  content: string;
}

interface WorkspaceState {
  activeWorkspace: Workspace | null;
  activeFile: ActiveFile;
  pendingInjection: string | null;
  viewMode: 'list' | 'tree';
  setActiveWorkspace: (workspace: Workspace) => void;
  clearActiveWorkspace: () => void;
  setActiveFile: (file: ActiveFile) => void;
  injectCode: (code: string) => void;
  clearInjection: () => void;
  setViewMode: (mode: 'list' | 'tree') => void;
  quotedSnippets: QuotedSnippet[];
  referencedFiles: string[];
  addQuotedSnippet: (snippet: QuotedSnippet) => void;
  removeQuotedSnippet: (id: string) => void;
  addReferencedFile: (fileName: string) => void;
  removeReferencedFile: (fileName: string) => void;
  clearContext: () => void;
}

export const useWorkspaceStore = create<WorkspaceState>()(
  persist(
    (set) => ({
      activeWorkspace: null,
      activeFile: { name: 'main.py', type: 'code' },
      pendingInjection: null,
      viewMode: 'list',
      setActiveWorkspace: (workspace: Workspace) => set({ activeWorkspace: workspace, activeFile: { name: 'main.py', type: 'code' } }),
      clearActiveWorkspace: () => set({ activeWorkspace: null, activeFile: { name: 'main.py', type: 'code' } }),
      setActiveFile: (file: ActiveFile) => set({ activeFile: file }),
      injectCode: (code: string) => set({ pendingInjection: code }),
      clearInjection: () => set({ pendingInjection: null }),
      setViewMode: (mode: 'list' | 'tree') => set({ viewMode: mode }),
      quotedSnippets: [],
      referencedFiles: [],
      addQuotedSnippet: (snippet) => set((state) => ({ quotedSnippets: [...state.quotedSnippets, snippet] })),
      removeQuotedSnippet: (id) => set((state) => ({ quotedSnippets: state.quotedSnippets.filter(s => s.id !== id) })),
      addReferencedFile: (fileName) => set((state) => ({ referencedFiles: state.referencedFiles.includes(fileName) ? state.referencedFiles : [...state.referencedFiles, fileName] })),
      removeReferencedFile: (fileName) => set((state) => ({ referencedFiles: state.referencedFiles.filter(f => f !== fileName) })),
      clearContext: () => set({ quotedSnippets: [], referencedFiles: [] }),
    }),
    {
      name: 'workspace-storage',
    }
  )
);
