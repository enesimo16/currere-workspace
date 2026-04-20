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
  name: string;
  type: string;
}

interface WorkspaceState {
  activeWorkspace: Workspace | null;
  activeFile: ActiveFile;
  setActiveWorkspace: (workspace: Workspace) => void;
  clearActiveWorkspace: () => void;
  setActiveFile: (file: ActiveFile) => void;
}

export const useWorkspaceStore = create<WorkspaceState>()(
  persist(
    (set) => ({
      activeWorkspace: null,
      activeFile: { name: 'main.py', type: 'code' },
      setActiveWorkspace: (workspace: Workspace) => set({ activeWorkspace: workspace, activeFile: { name: 'main.py', type: 'code' } }),
      clearActiveWorkspace: () => set({ activeWorkspace: null, activeFile: { name: 'main.py', type: 'code' } }),
      setActiveFile: (file: ActiveFile) => set({ activeFile: file }),
    }),
    {
      name: 'workspace-storage',
    }
  )
);
