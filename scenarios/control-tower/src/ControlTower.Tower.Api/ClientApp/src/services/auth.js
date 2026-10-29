import { UserManager, WebStorageStateStore, Log } from 'oidc-client'
import axios from 'axios'

// Sign-in against the Proseware token service (authorization code + PKCE).
// The settings come from /api/config so the same bundle works in every environment.

let manager = null
let config = null
let currentUser = null

export async function initAuth() {
  const { data } = await axios.get('/api/config')
  config = data
  if (config.developmentSignIn) {
    currentUser = { profile: { name: 'dev.operator', role: ['ops', 'admin'] }, access_token: null }
    return
  }
  Log.logger = console
  Log.level = Log.WARN
  manager = new UserManager({
    authority: config.authority,
    client_id: config.clientId,
    redirect_uri: window.location.origin + '/app/callback',
    post_logout_redirect_uri: window.location.origin + '/app/',
    response_type: 'code',
    scope: config.scope,
    automaticSilentRenew: true,
    silent_redirect_uri: window.location.origin + '/app/silent-renew.html',
    loadUserInfo: true,
    userStore: new WebStorageStateStore({ store: window.sessionStorage })
  })
  manager.events.addUserLoaded((u) => { currentUser = u })
  manager.events.addUserSignedOut(() => { currentUser = null })
  manager.events.addSilentRenewError((e) => console.warn('Silent renew failed', e))
  currentUser = await manager.getUser()
}

export function appConfig() {
  return config
}

export function user() {
  return currentUser
}

export function accessToken() {
  return currentUser && currentUser.access_token
}

export function displayName() {
  return (currentUser && currentUser.profile && (currentUser.profile.name || currentUser.profile.sub)) || ''
}

export async function ensureSignedIn(returnPath) {
  if (!manager) return true
  if (currentUser && !currentUser.expired) return true
  await manager.signinRedirect({ state: { returnPath } })
  return false
}

export async function completeSignIn() {
  if (!manager) return '/'
  const u = await manager.signinRedirectCallback()
  currentUser = u
  return (u.state && u.state.returnPath) || '/'
}

export async function signOut() {
  if (!manager) return
  await manager.signoutRedirect()
}
