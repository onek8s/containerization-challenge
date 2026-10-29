import { defineStore } from 'pinia'
import { on } from '../services/live'

// Recent live events for the feed in the header and the overview page.
export const useLiveStore = defineStore('live', {
  state: () => ({
    events: [],
    newAlerts: 0,
    started: false
  }),
  actions: {
    start() {
      if (this.started) return
      this.started = true
      on('Milestone', (m) => this.push('milestone', `${m.number}: ${m.description}${m.location ? ' – ' + m.location : ''}`, m.shipmentId))
      on('EtaUpdated', (m) => this.push('eta', `${m.number}: ETA ${new Date(m.eta).toLocaleString('en-GB')}`, m.shipmentId))
      on('AlertRaised', (m) => {
        this.newAlerts++
        this.push('alert-' + String(m.severity).toLowerCase(), `${m.number}: ${m.message}`, m.shipmentId)
      })
      on('AlertClosed', (m) => this.push('closed', `Alert ${m.alertId} closed by ${m.closedBy}`, m.shipmentId))
    },
    push(kind, text, shipmentId) {
      this.events.unshift({ id: Date.now() + Math.random(), kind, text, shipmentId, at: new Date() })
      if (this.events.length > 100) this.events.length = 100
    },
    clearAlerts() {
      this.newAlerts = 0
    }
  }
})
