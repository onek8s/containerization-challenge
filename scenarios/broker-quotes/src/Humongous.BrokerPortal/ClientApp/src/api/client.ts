import type { ProblemDetails } from './types';

export class ApiError extends Error {
  constructor(public status: number, public problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: {
      'X-CSRF': '1',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {})
    },
    body: body !== undefined ? JSON.stringify(body) : undefined
  });

  if (response.status === 401) {
    window.location.href = `/bff/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
    throw new ApiError(401, { title: 'Signed out' });
  }
  if (!response.ok) {
    let problem: ProblemDetails = { title: response.statusText };
    try {
      problem = await response.json();
    } catch {
      /* not JSON */
    }
    throw new ApiError(response.status, problem);
  }
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T>(url: string, body: unknown) => request<T>('PUT', url, body)
};
