import { LogLevel, OpenIdConfiguration, StsConfigLoader, StsConfigStaticLoader } from 'angular-auth-oidc-client';
import { AppConfigHolder } from './app-config';

export function oidcConfigLoaderFactory(): StsConfigLoader {
  const auth = AppConfigHolder.config.auth;
  const config: OpenIdConfiguration = {
    authority: auth.authority,
    redirectUrl: window.location.origin + '/',
    postLogoutRedirectUri: window.location.origin + '/',
    clientId: auth.clientId,
    scope: auth.scope,
    responseType: 'code',
    silentRenew: true,
    useRefreshToken: true,
    renewTimeBeforeTokenExpiresInSeconds: 60,
    // ADFS issues the access token for the relying party named in the resource parameter.
    customParamsAuthRequest: { resource: auth.resource },
    customParamsCodeRequest: { resource: auth.resource },
    customParamsRefreshTokenRequest: { resource: auth.resource },
    // The user's name and UPN come from the API (/api/me); ADFS userinfo returns only the subject.
    autoUserInfo: false,
    // Workstation clocks drift more than the library's default allows.
    maxIdTokenIatOffsetAllowedInSeconds: 600,
    logLevel: LogLevel.Warn
  };
  return new StsConfigStaticLoader(config);
}
