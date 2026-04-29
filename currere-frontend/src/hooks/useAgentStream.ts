import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAgentStore } from '../store/useAgentStore';

interface UseAgentStreamProps {
  workspaceId: number | null;
  hubUrl?: string; // Projedeki backend .env url'ine göre dışarıdan alınabilir
}

export const useAgentStream = ({ 
  workspaceId, 
  hubUrl = 'http://localhost:5000/terminalHub' // Projenin mevcut backend portuna göre ayarlanmalıdır
}: UseAgentStreamProps) => {
  const { setStatus, addLog } = useAgentStore();
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    // Workspace ID yoksa dinleme yapmaya gerek yok
    if (!workspaceId) return;

    // Halihazırda açık bir bağlantı varsa (strict mode veya re-render kaynaklı), durdur.
    if (connectionRef.current) {
      connectionRef.current.stop();
    }

    // Yeni bağlantıyı inşa et
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    const startConnection = async () => {
      try {
        await connection.start();
        console.log(`[Agent Stream] SignalR connected for Workspace: ${workspaceId}`);

        // Eğer backend tarafında TerminalHub'a bağlanırken belirli bir gruba (WorkspaceId grubuna)
        // dahil olma metodunuz varsa, bunu çağırın. Örn:
        // await connection.invoke("JoinGroup", workspaceId.toString());

        // Backend AgentOrchestrator'dan gelen canlı durum mesajlarını dinle
        connection.on("ReceiveAgentStatus", (message: string) => {
          setStatus(message);
          addLog(message);
        });

      } catch (error) {
        console.error('[Agent Stream] SignalR Connection Failed:', error);
      }
    };

    startConnection();

    // Cleanup: Component unmount olduğunda eventleri ve bağlantıyı temizle
    return () => {
      if (connectionRef.current) {
        connectionRef.current.off("ReceiveAgentStatus");
        connectionRef.current.stop();
        connectionRef.current = null;
      }
    };
  }, [workspaceId, hubUrl, setStatus, addLog]);

  return {
    connection: connectionRef.current
  };
};
