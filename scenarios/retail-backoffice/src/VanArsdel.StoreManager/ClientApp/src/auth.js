import { UserManager, WebStorageStateStore } from 'oidc-client';
import { getConfig } from './config';

let manager = null;

export function userManager() {
  if (!manager) {
    const c = getConfig();
    manager = new UserManager({
      authority: c.authority,
      client_id: c.clientId,
      redirect_uri: window.location.origin + '/callback',
      post_logout_redirect_uri: window.location.origin + '/',
      response_type: 'code',
      scope: c.scope,
      loadUserInfo: false,
      automaticSilentRenew: false,
      userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    });
  }
  return manager;
}

export async function currentUser() {
  const user = await userManager().getUser();
  return user && !user.expired ? user : null;
}

export function storeCodeOf(user) {
  if (!user) return null;
  return user.profile.store_code || user.profile.extension_StoreCode || null;
}
