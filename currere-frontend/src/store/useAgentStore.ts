import { create } from 'zustand';

interface AgentState {
  isAgentActive: boolean;
  currentStatus: string;
  agentLogs: string[];
  setStatus: (status: string) => void;
  addLog: (log: string) => void;
  resetAgentState: () => void;
}

export const useAgentStore = create<AgentState>((set) => ({
  isAgentActive: false,
  currentStatus: '',
  agentLogs: [],
  
  // Ajanın anlık durumunu günceller ve ajanı otomatik 'Aktif' statüsüne alır
  setStatus: (status: string) => 
    set({ 
      currentStatus: status, 
      isAgentActive: true 
    }),
    
  // Gelen logları (UI'da tamamen gösterilmese bile) geçmişi tutmak adına kaydeder
  addLog: (log: string) => 
    set((state) => ({ 
      agentLogs: [...state.agentLogs, log] 
    })),
    
  // Görev tamamlandığında veya iptal edildiğinde sistemi sıfırlar
  resetAgentState: () => 
    set({ 
      isAgentActive: false, 
      currentStatus: '', 
      agentLogs: [] 
    }),
}));
