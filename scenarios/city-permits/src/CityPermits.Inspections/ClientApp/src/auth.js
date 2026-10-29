import axios from 'axios'
import { UserManager, WebStorageStateStore } from 'oidc-client'

let manager = null
export const auth = {
  config: null,
  token: localStorage.getItem('insp.token'),
  async init () {
    this.config = (await axios.get('/api/config')).data
    if (this.config.devMode) return
    manager = new UserManager({
      authority: this.config.authority,
      client_id: this.config.clientId,
      redirect_uri: window.location.origin + '/',
      response_type: 'code',
      scope: 'openid profile allatclaims',
      userStore: new WebStorageStateStore({ store: window.localStorage })
    })
    if (window.location.search.includes('code=')) {
      await manager.signinRedirectCallback()
      window.history.replaceState({}, '', '/')
    }
    const user = await manager.getUser()
    if (!user || user.expired) return manager.signinRedirect()
    this.token = user.access_token
  },
  async devLogin (user) {
    const r = await axios.post('/api/dev-token', { user })
    this.token = r.data.access_token
    localStorage.setItem('insp.token', this.token)
  },
  http () {
    return axios.create({ headers: { Authorization: 'Bearer ' + this.token } })
  }
}
