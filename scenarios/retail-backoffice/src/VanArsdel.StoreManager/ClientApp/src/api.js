import { getConfig } from './config';
import { currentUser, userManager } from './auth';

// All calls go to the store operations service through the gateway.
export async function api(path, options = {}) {
  const user = await currentUser();
  if (!user) {
    await userManager().signinRedirect({ state: window.location.pathname });
    return null;
  }
  const response = await fetch(getConfig().apiBaseUrl + path, {
    ...options,
    headers: {
      Authorization: 'Bearer ' + user.access_token,
      'Content-Type': 'application/json',
      ...(options.headers || {}),
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });
  if (response.status === 401) {
    await userManager().signinRedirect({ state: window.location.pathname });
    return null;
  }
  if (!response.ok) {
    let message = response.status + ' ' + response.statusText;
    try {
      const problem = await response.json();
      message = problem.detail || problem.title || message;
    } catch (e) {
      // not JSON
    }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}

export const money = (value) =>
  new Intl.NumberFormat('fi-FI', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value || 0);
