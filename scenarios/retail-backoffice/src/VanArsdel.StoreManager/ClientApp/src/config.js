// Runtime configuration from the host (/config.json), so one build works in every environment.
let config = null;

export async function loadConfig() {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) throw new Error('Could not load /config.json');
  config = await response.json();
  return config;
}

export function getConfig() {
  return config;
}
