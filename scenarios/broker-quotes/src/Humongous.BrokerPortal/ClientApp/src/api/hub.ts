import { useEffect, useRef } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { DocumentNotice, PolicyNotice, QuoteNotice } from './types';

let connection: HubConnection | null = null;
let starting: Promise<void> | null = null;

// One connection per browser tab, through the portal host (the session cookie is sent; the host adds the token).
function getConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/quotes')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
  }
  return connection;
}

async function ensureStarted(): Promise<HubConnection> {
  const c = getConnection();
  if (c.state === 'Disconnected') {
    starting ??= c.start().finally(() => (starting = null));
    await starting;
  } else if (starting) {
    await starting;
  }
  return c;
}

export interface HubHandlers {
  quoteChanged?: (n: QuoteNotice) => void;
  policyChanged?: (n: PolicyNotice) => void;
  documentReady?: (n: DocumentNotice) => void;
}

export function useQuoteHub(handlers: HubHandlers, watchQuoteId?: string) {
  const ref = useRef(handlers);
  ref.current = handlers;

  useEffect(() => {
    const c = getConnection();
    const onQuote = (n: QuoteNotice) => ref.current.quoteChanged?.(n);
    const onPolicy = (n: PolicyNotice) => ref.current.policyChanged?.(n);
    const onDocument = (n: DocumentNotice) => ref.current.documentReady?.(n);
    c.on('QuoteChanged', onQuote);
    c.on('PolicyChanged', onPolicy);
    c.on('DocumentReady', onDocument);
    ensureStarted()
      .then(h => (watchQuoteId ? h.invoke('WatchQuote', watchQuoteId) : undefined))
      .catch(err => console.warn('Live updates unavailable', err));
    return () => {
      c.off('QuoteChanged', onQuote);
      c.off('PolicyChanged', onPolicy);
      c.off('DocumentReady', onDocument);
      if (watchQuoteId && c.state === 'Connected') c.invoke('UnwatchQuote', watchQuoteId).catch(() => undefined);
    };
  }, [watchQuoteId]);
}
