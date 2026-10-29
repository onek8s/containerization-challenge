import { auth } from './auth'

// Results recorded without signal are kept on the device and sent when the connection returns.
const KEY = 'insp.queue'
export const queue = {
  items () { return JSON.parse(localStorage.getItem(KEY) || '[]') },
  save (items) { localStorage.setItem(KEY, JSON.stringify(items)) },
  add (result) { const i = this.items(); i.push(result); this.save(i) },
  async flush () {
    const pending = this.items()
    const left = []
    for (const r of pending) {
      try { await auth.http().post('/api/results', r) } catch (e) { if (!e.response || e.response.status >= 500) left.push(r) }
    }
    this.save(left)
    return pending.length - left.length
  }
}
window.addEventListener('online', () => queue.flush())
