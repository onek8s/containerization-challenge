export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (res.status === 401) {
    // Session expired: reload so the server sends the browser to ADFS.
    window.location.reload();
    throw new ApiError(401, "Signed out");
  }
  if (!res.ok) {
    let message = res.statusText;
    try {
      const problem = await res.json();
      message = problem.detail ?? problem.title ?? message;
    } catch {
      /* not json */
    }
    throw new ApiError(res.status, message);
  }
  if (res.status === 204) return undefined as T;
  const type = res.headers.get("content-type") ?? "";
  return (type.includes("json") ? res.json() : res.text()) as Promise<T>;
}

export const api = {
  get: <T>(url: string) => request<T>("GET", url),
  post: <T>(url: string, body: unknown = {}) => request<T>("POST", url, body)
};

export interface Study { id: number; code: string; title: string; sponsor: string; phase: string; status: string; targetEnrolment: number }
export interface SiteMetrics {
  id: number; number: string; name: string; investigator: string; country: string;
  subjects: number; randomised: number; valuesToVerify: number; openQueries: number; answeredQueries: number;
}
export interface SubjectRow { id: number; number: string; status: string; consentDate?: string; randomisationNumber?: string }
export interface Field {
  code: string; label: string; type: string; unit?: string; sdv: boolean; value?: string; raw?: string;
  verified: boolean; verifiedBy?: string; coding?: { state: string; code?: string; term?: string; hierarchy?: string } | null;
}
export interface QueryMessage { at: string; user: string; role: string; text: string }
export interface FormQuery { id: number; fieldCode: string; state: string; origin: string; text: string; openedAt: string; openedBy: string; messages: QueryMessage[] }
export interface Form {
  id: number; code: string; name: string; row: number; status: string; lastSavedAt?: string; lastSavedBy?: string;
  signedBy?: string; derivedWith?: string; fields: Field[]; queries: FormQuery[];
}
export interface Visit { id: number; code: string; name: string; date?: string; status: string; outOfWindow: boolean; forms: Form[] }
export interface SubjectDetail {
  id: number; number: string; status: string; randomisationNumber?: string; consentDate?: string;
  site: { id: number; number: string; name: string }; study: { id: number; code: string }; visits: Visit[];
}
export interface QueryRow {
  id: number; subjectId: number; subject: string; site: string; visit: string; form: string; row: number; fieldCode: string;
  state: string; origin: string; text: string; openedAt: string; openedBy: string; ageDays: number; formInstanceId: number;
}
export interface CodingRow { id: number; dictionary: string; verbatim: string; state: string; code?: string; term?: string; score?: number; subject: string; subjectId: number }
export interface Candidate { code: string; term: string; preferredCode: string; preferredTerm: string; hierarchy: string; score: number }
export interface LabRow { visitCode: string; collectedAt: string; testCode: string; testName: string; value?: string; unit?: string; lowerLimit?: number; upperLimit?: number; flag: string; grade?: number; accession: string }
export interface Notice { id: number; at: string; kind: string; text: string; subjectId?: number; siteId?: number }
