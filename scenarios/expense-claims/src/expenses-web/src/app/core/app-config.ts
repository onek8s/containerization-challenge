export interface AuthConfig {
  /** "adfs" signs in with ADFS (OpenID Connect, code flow with PKCE); "development" uses the API's test sign-in. */
  mode: 'adfs' | 'development';
  authority: string;
  clientId: string;
  resource: string;
  scope: string;
}

export interface AppConfig {
  apiUrl: string;
  auth: AuthConfig;
}

/** Runtime configuration from assets/config.json, written by the deployment script. */
export class AppConfigHolder {
  static config: AppConfig;
}

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('assets/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`assets/config.json could not be loaded (${response.status})`);
  }
  AppConfigHolder.config = await response.json();
  return AppConfigHolder.config;
}
