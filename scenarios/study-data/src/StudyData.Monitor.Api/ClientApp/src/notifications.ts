import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useEffect, useState } from "react";
import { api, Notice } from "./api";

let connection: HubConnection | null = null;

function hub(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl("/hubs/notifications")
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
  }
  return connection;
}

/** Recent notifications of a study, kept up to date over SignalR. */
export function useNotifications(studyId: number | undefined): Notice[] {
  const [items, setItems] = useState<Notice[]>([]);

  useEffect(() => {
    if (!studyId) return;
    let active = true;
    api.get<Notice[]>(`/api/notifications?studyId=${studyId}`).then(n => active && setItems(n));

    const c = hub();
    const onNotice = (n: Notice) => setItems(prev => [n, ...prev].slice(0, 50));
    c.on("notification", onNotice);
    const start = c.state === "Disconnected" ? c.start() : Promise.resolve();
    start.then(() => c.invoke("Follow", studyId)).catch(() => undefined);
    return () => {
      active = false;
      c.off("notification", onNotice);
      if (c.state === "Connected") c.invoke("Unfollow", studyId).catch(() => undefined);
    };
  }, [studyId]);

  return items;
}
