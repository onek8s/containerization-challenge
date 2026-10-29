<template>
  <p><router-link to="/shipments">← Shipments</router-link></p>
  <p v-if="error" class="error">{{ error }}</p>
  <template v-if="s">
    <h1>{{ s.number }} <StatusBadge :status="s.status" /></h1>
    <div class="grid two">
      <div class="panel">
        <table>
          <tr><th>Customer</th><td>{{ s.customer }} <span class="muted">{{ s.customerReference }}</span></td></tr>
          <tr><th>Mode / Incoterm</th><td>{{ s.mode }} · {{ s.incoterm }}</td></tr>
          <tr><th>Shipper</th><td>{{ s.shipper }}<br /><span class="muted">{{ s.origin }}</span></td></tr>
          <tr><th>Consignee</th><td>{{ s.consignee }}<br /><span class="muted">{{ s.destination }}</span></td></tr>
          <tr><th>Pickup</th><td>{{ fmtDateTime(s.plannedPickup) }} <span class="muted">actual {{ fmtDateTime(s.actualPickup) }}</span></td></tr>
          <tr><th>Delivery</th><td>{{ fmtDateTime(s.plannedDelivery) }} <span class="muted">actual {{ fmtDateTime(s.actualDelivery) }}</span></td></tr>
          <tr><th>ETA</th><td :class="{ late: late }">{{ fmtDateTime(s.eta) }}</td></tr>
          <tr><th>Goods</th><td>{{ s.goods }} · {{ s.packages }} pcs · {{ fmtNumber(s.grossWeightKg, 1) }} kg · {{ fmtNumber(s.volumeM3, 2) }} m³</td></tr>
          <tr v-if="s.temperatureMin !== null || s.temperatureMax !== null"><th>Temperature</th><td>{{ s.temperatureMin }} … {{ s.temperatureMax }} °C</td></tr>
          <tr v-if="s.dangerousGoods"><th>Dangerous goods</th><td><span class="pill warning">DG</span></td></tr>
        </table>

        <h2 style="margin-top: 16px">Legs</h2>
        <table>
          <thead><tr><th>#</th><th>Carrier</th><th>From → To</th><th>Planned</th><th>Actual / ETA</th><th>Reference</th></tr></thead>
          <tr v-for="l in s.legs" :key="l.id">
            <td>{{ l.sequence }}</td>
            <td>{{ l.carrier }}<br /><span class="muted">{{ l.mode }} {{ l.vesselOrFlight }} {{ l.vehicle }}</span></td>
            <td>{{ l.from }} → {{ l.to }}<br /><span class="pill">{{ l.status }}</span></td>
            <td>{{ fmtDateTime(l.plannedDeparture) }}<br />{{ fmtDateTime(l.plannedArrival) }}</td>
            <td>{{ fmtDateTime(l.actualDeparture) }}<br /><span :class="{ late: legLate(l) }">{{ fmtDateTime(l.actualArrival || l.eta) }}</span></td>
            <td><a v-if="l.trackingUrl" :href="l.trackingUrl" target="_blank" rel="noopener">{{ l.carrierReference }}</a><span v-else>{{ l.carrierReference }}</span></td>
          </tr>
        </table>

        <h2 style="margin-top: 16px">Packages</h2>
        <table>
          <thead><tr><th>SSCC</th><th>Kind</th><th>Weight</th><th>Dimensions</th><th>DG / container</th></tr></thead>
          <tr v-for="p in s.packageList" :key="p.sscc">
            <td><code>{{ p.sscc }}</code></td><td>{{ p.kind }}</td><td>{{ fmtNumber(p.weightKg, 1) }} kg</td><td>{{ p.dimensions }}</td>
            <td>{{ p.dangerousGoods || p.containerNumber }}</td>
          </tr>
        </table>

        <template v-if="s.customs.length">
          <h2 style="margin-top: 16px">Customs</h2>
          <table>
            <tr v-for="c in s.customs" :key="c.id">
              <td>{{ c.type }}</td><td><span class="pill">{{ c.status }}</span></td><td>LRN {{ c.lrn }}</td><td>MRN {{ c.mrn || '–' }}</td>
              <td>{{ fmtDateTime(c.submittedAt) }} → {{ fmtDateTime(c.releasedAt) }}</td><td class="error">{{ c.rejectionReason }}</td>
            </tr>
          </table>
        </template>
      </div>

      <div>
        <div class="panel">
          <h2>Timeline</h2>
          <ul class="timeline">
            <li v-for="(m, i) in s.milestones" :key="i">
              <b>{{ m.description }}</b> <span class="muted">{{ fmtDateTime(m.occurredAt) }}</span><br />
              <span class="muted">{{ m.location }} · {{ m.source }}</span>
              <div v-if="m.remarks">{{ m.remarks }}</div>
            </li>
          </ul>
        </div>
        <div class="panel" style="margin-top: 12px">
          <h2>Alerts</h2>
          <p v-if="!s.alerts.length" class="muted">No alerts.</p>
          <div v-for="a in s.alerts" :key="a.id" style="margin-bottom: 8px">
            <SeverityBadge :severity="a.severity" /> <b>{{ a.ruleCode }}</b>
            <span v-if="a.resolvedAt" class="pill ok">resolved</span>
            <div>{{ a.message }}</div>
            <div class="muted">{{ fmtDateTime(a.raisedAt) }}<span v-if="a.resolution"> · {{ a.resolution }}</span></div>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import api from '../services/api'
import { liveConnection, on, off } from '../services/live'
import { fmtDateTime, fmtNumber } from '../services/format'
import StatusBadge from '../components/StatusBadge.vue'
import SeverityBadge from '../components/SeverityBadge.vue'

const props = defineProps({ id: { type: String, required: true } })
const s = ref(null)
const error = ref(null)

const late = computed(() => s.value && s.value.eta && new Date(s.value.eta) - new Date(s.value.plannedDelivery) > 2 * 3600000)

function legLate(l) {
  const t = l.actualArrival || l.eta
  return t && new Date(t) - new Date(l.plannedArrival) > 3600000
}

async function load() {
  try {
    s.value = await api.shipment(props.id)
    error.value = null
  } catch (e) {
    error.value = e.response && e.response.status === 404 ? 'Shipment not found.' : 'Could not load the shipment: ' + e.message
  }
}

function onEvent(m) {
  if (String(m.shipmentId) === String(props.id)) load()
}

onMounted(() => {
  load()
  const c = liveConnection()
  c.invoke('WatchShipment', Number(props.id)).catch(() => {})
  for (const n of ['Milestone', 'EtaUpdated', 'AlertRaised', 'AlertClosed']) on(n, onEvent)
})
watch(() => props.id, load)
onUnmounted(() => {
  for (const n of ['Milestone', 'EtaUpdated', 'AlertRaised', 'AlertClosed']) off(n, onEvent)
  liveConnection().invoke('UnwatchShipment', Number(props.id)).catch(() => {})
})
</script>
