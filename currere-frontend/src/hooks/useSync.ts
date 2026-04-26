import { useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

export const useSync = (workspaceId: number | string | undefined) => {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const mountedRef = useRef(false);
  const connectingRef = useRef(false);

  const { activeFile } = useWorkspaceStore();
  const activeFileNameRef = useRef(activeFile.name);

  // Ref update to avoid stale closures in SignalR callbacks
  useEffect(() => {
    activeFileNameRef.current = activeFile.name;
  }, [activeFile.name]);

  useEffect(() => {
    if (!workspaceId) return;

    // STRICT MODE GUARD: Zaten bağlıysa veya bağlanma işlemi devam ediyorsa çık
    if (connectingRef.current) return;
    if (
      connectionRef.current &&
      connectionRef.current.state === signalR.HubConnectionState.Connected
    ) {
      return;
    }

    mountedRef.current = true;
    connectingRef.current = true;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(
        `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5279'}/syncHub`,
        {
          // skipNegotiation KALDIRILDI — negotiate endpoint üzerinden gidecek
          // Bu sayede CORS preflight düzgün çalışır ve 1006 hatası önlenir
          transport:
            signalR.HttpTransportType.WebSockets |
            signalR.HttpTransportType.ServerSentEvents |
            signalR.HttpTransportType.LongPolling,
        }
      )
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    // --- EVENT LISTENERS ---

    connection.on('ReceiveCodeUpdate', (fileName: string, content: string) => {
      if (fileName === activeFileNameRef.current && mountedRef.current) {
        window.dispatchEvent(
          new CustomEvent('editor-sync-update', { detail: content })
        );
      }
    });

    connection.onreconnecting((error) => {
      console.warn('[Sync] Bağlantı yeniden kuruluyor...', error?.message);
    });

    connection.onreconnected(async (connectionId) => {
      console.log('[Sync] Yeniden bağlandı:', connectionId);
      try {
        await connection.invoke('JoinWorkspaceById', Number(workspaceId));
      } catch (err) {
        console.error('[Sync] Yeniden bağlanma sonrası gruba katılma hatası:', err);
      }
    });

    connection.onclose((error) => {
      if (mountedRef.current) {
        console.warn('[Sync] Bağlantı kapandı.', error?.message);
      }
      // connectionRef temizle — ileride tekrar mount olursa yeni bağlantı kurulsun
      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
    });

    // --- CONNECTION START ---

    const startConnection = async () => {
      try {
        await connection.start();

        // Strict Mode cleanup'ı start() bitmeden çağrıldıysa, bağlantıyı kapat
        if (!mountedRef.current) {
          await connection.stop().catch(() => {});
          connectingRef.current = false;
          return;
        }

        connectionRef.current = connection;
        console.log('[Sync] Hub bağlantısı kuruldu.');
        await connection.invoke('JoinWorkspaceById', Number(workspaceId));
      } catch (err: unknown) {
        // Strict Mode'un çift render'ı negotiation sırasında stop() çağırabilir.
        // Bu beklenen bir davranış — sessizce yut, ikinci render bağlantıyı kuracak.
        const errMsg = err instanceof Error ? err.message : String(err);
        const isExpectedAbort =
          errMsg.includes('negotiation') ||
          errMsg.includes('abort') ||
          errMsg.includes('stopped') ||
          errMsg.includes('The connection was stopped');

        if (mountedRef.current && !isExpectedAbort) {
          console.error('[Sync] Hub bağlantı hatası:', err);
        }
      } finally {
        connectingRef.current = false;
      }
    };

    startConnection();

    // --- CLEANUP ---

    return () => {
      mountedRef.current = false;

      // Eğer hâlâ bağlanıyorsak, startConnection içindeki guard yakalayacak.
      // Eğer bağlıysa, güvenle durdur.
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        connection.stop().catch(() => {});
      }

      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
    };
  }, [workspaceId]);

  const sendUpdate = useCallback(
    (fileName: string, content: string) => {
      const conn = connectionRef.current;
      if (conn && conn.state === signalR.HubConnectionState.Connected) {
        conn
          .invoke('SendCodeUpdate', Number(workspaceId), fileName, content)
          .catch((err) => console.error('[Sync] Gönderim hatası:', err));
      }
    },
    [workspaceId]
  );

  return { sendUpdate };
};
