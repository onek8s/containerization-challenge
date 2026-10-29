import { UserManager, WebStorageStateStore } from 'oidc-client'

new UserManager({ userStore: new WebStorageStateStore({ store: window.sessionStorage }) })
  .signinSilentCallback()
  .catch((e) => console.error(e))
