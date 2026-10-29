import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { getConfig } from './config';
import { currentUser } from './auth';

// One hub connection per tab, joined to the user's own store.
let connection = null;

export async function connectLive(storeCode, handlers) {
  if (connection) await connection.stop();
  connection = new HubConnectionBuilder()
    .withUrl(getConfig().hubUrl, {
      accessTokenFactory: async () => {
        const user = await currentUser();
        return user ? user.access_token : '';
      },
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  Object.keys(handlers).forEach((name) => connection.on(name, handlers[name]));
  connection.onreconnected(() => connection.invoke('JoinStore', storeCode));
  await connection.start();
  await connection.invoke('JoinStore', storeCode);
  return connection;
}
