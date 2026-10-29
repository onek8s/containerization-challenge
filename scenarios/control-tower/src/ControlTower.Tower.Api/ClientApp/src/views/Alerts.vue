<template>
  <h1>Alerts</h1>
  <div class="toolbar">
    <label><input type="checkbox" v-model="openOnly" @change="load" /> Open only</label>
    <select v-model="minSeverity" @change="load">
      <option value="">All severities</option>
      <option value="Warning">Warning and critical</option>
      <option value="Critical">Critical only</option>
    </select>
    <button @click="load">Refresh</button>
    <span class="muted">{{ alerts.length }} alerts</span>
  </div>
  <p v-if="error" class="error">{{ error }}</p>
  <div class="panel">
    <table>
      <thead>
        <tr><th>Severity</th><th>Shipment</th><th>Rule</th><th>Message</th><th>Raised</th><th>Seen</th><th>Handled</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="a in alerts" :key="a.id">
          <td><SeverityBadge :severity="a.severity" /></td>
          <td><router-link :to="'/shipments/' + a.shipmentId">{{ a.shipmentNumber }}</router-link><br /><span class="muted">{{ a.customer }}</span></td>
          <td><code>{{ a.ruleCode }}</code></td>
          <td>{{ a.message }}</td>
          <td>{{ fmtDateTime(a.raisedAt) }}</td>
          <td>{{ relative(a.lastSeenAt) }}<span v-if="a.occurrences > 1" class="muted"> ×{{ a.occurrences }}</span></td>
          <td>
            <span v-if="a.resolvedAt">Resolved {{ fmtDateTime(a.resolvedAt) }}<br /><span class="muted">{{ a.resolution }}</span></span>
            <span v-else-if="a.acknowledgedAt">Ack {{ a.acknowledgedBy }}</span>
          </td>
          <td style="white-space: nowrap">
            <template v-if="!a.resolvedAt">
              <button v-if="!a.acknowledgedAt" @click="ack(a)">Acknowledge</button>
              <button class="primary" @click="resolve(a)">Resolve</button>
            </template>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import api from '../services/api'
import { on, off } from '../services/live'
import { fmtDateTime, relative } from '../services/format'
import SeverityBadge from '../components/SeverityBadge.vue'

const alerts = ref([])
const openOnly = ref(true)
const minSeverity = ref('')
const error = ref(null)

async function load() {
  try {
    alerts.value = await api.alerts({ openOnly: openOnly.value, minSeverity: minSeverity.value || undefined, take: 500 })
    error.value = null
  } catch (e) {
    error.value = 'Could not load alerts: ' + e.message
  }
}

function replace(row) {
  const i = alerts.value.findIndex((a) => a.id === row.id)
  if (i >= 0) alerts.value.splice(i, 1, row)
}

async function ack(a) {
  replace(await api.acknowledge(a.id))
}

async function resolve(a) {
  const text = window.prompt('Resolution', 'Customer informed')
  if (text === null) return
  replace(await api.resolve(a.id, text))
}

function onChange() {
  load()
}

onMounted(() => {
  load()
  on('AlertRaised', onChange)
  on('AlertClosed', onChange)
})
onUnmounted(() => {
  off('AlertRaised', onChange)
  off('AlertClosed', onChange)
})
</script>
