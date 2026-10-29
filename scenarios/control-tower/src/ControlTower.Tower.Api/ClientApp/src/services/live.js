import { HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr'
import { accessToken } from './auth'

// One hub connection for the whole app; views register handlers with on()/off().
let connection = null
const handlers = {}
const stateListeners = new Set()

function notifyState() {
  const s = connection ? connection.state : HubConnectionState.Disconnected
  stateListeners.forEach((fn) => fn(s))
}

export function liveConnection() {
  if (connection) return connection
  connection = new HubConnectionBuilder()
    .withUrl('/hubs/tower', { accessTokenFactory: () => accessToken() || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 60000])
    .configureLogging(LogLevel.Warning)
    .build()

  for (const name of ['Milestone', 'AlertRaised', 'AlertClosed', 'EtaUpdated', 'VehiclePosition']) {
    connection.on(name, (msg) => (handlers[name] || []).forEach((h) => h(msg)))
  }
  connection.onreconnecting(notifyState)
  connection.onreconnected(notifyState)
  connection.onclose(() => {
    notifyState()
    setTimeout(start, 15000)
  })
  start()
  return connection
}

async function start() {
  if (connection.state !== HubConnectionState.Disconnected) return
  try {
    await connection.start()
  } catch (e) {
    console.warn('Live updates unavailable, retrying', e)
    setTimeout(start, 10000)
  }
  notifyState()
}

export function on(name, handler) {
  liveConnection()
  ;(handlers[name] = handlers[name] || []).push(handler)
}

export function off(name, handler) {
  handlers[name] = (handlers[name] || []).filter((h) => h !== handler)
}

export function onState(fn) {
  stateListeners.add(fn)
  return () => stateListeners.delete(fn)
}
