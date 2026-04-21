import { useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

export const useSync = (workspaceId: number | string | undefined) => {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const { activeFile, setCode } = useWorkspaceStore();
  const activeFileNameRef = useRef(activeFile.name);

  // Ref update to avoid stale closures in SignalR callbacks
  useEffect(() => {
    activeFileNameRef.current = activeFile.name;
  }, [activeFile.name]);

  const startPromiseRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    if (!workspaceId) return;

    let isMounted = true;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5279'}/syncHub`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveCodeUpdate', (fileName: string, content: string) => {
      if (fileName === activeFileNameRef.current && isMounted) {
        window.dispatchEvent(new CustomEvent('editor-sync-update', { detail: content }));
      }
    });

    const startConnection = async () => {
      if (connection.state !== signalR.HubConnectionState.Disconnected) return;
      
      try {
        startPromiseRef.current = connection.start();
        await startPromiseRef.current;
        
        if (isMounted) {
          console.log('[Sync] Hub connection established.');
          await connection.invoke('JoinWorkspaceById', Number(workspaceId));
        }
      } catch (err) {
        if (isMounted) console.error('[Sync] Hub Connection Error:', err);
      }
    };

    startConnection();
    connectionRef.current = connection;

    return () => {
      isMounted = false;
      const stopConnection = async () => {
        if (startPromiseRef.current) {
          try { await startPromiseRef.current; } catch {}
        }
        if (connection.state !== signalR.HubConnectionState.Disconnected) {
          try { await connection.stop(); } catch {}
        }
      };
      stopConnection();
    };
  }, [workspaceId]);

  const sendUpdate = useCallback((fileName: string, content: string) => {
    if (connectionRef.current && connectionRef.current.state === signalR.HubConnectionState.Connected) {
      connectionRef.current.invoke('SendCodeUpdate', Number(workspaceId), fileName, content)
        .catch(err => console.error('[Sync] Send error:', err));
    }
  }, [workspaceId]);

  return { sendUpdate };
};
