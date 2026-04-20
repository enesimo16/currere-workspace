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

interface WorkspaceState {
  activeWorkspace: Workspace | null;
  setActiveWorkspace: (workspace: Workspace) => void;
  clearActiveWorkspace: () => void;
}

export const useWorkspaceStore = create<WorkspaceState>()(
  persist(
    (set) => ({
      activeWorkspace: null,
      setActiveWorkspace: (workspace: Workspace) => set({ activeWorkspace: workspace }),
      clearActiveWorkspace: () => set({ activeWorkspace: null }),
    }),
    {
      name: 'workspace-storage',
    }
  )
);
