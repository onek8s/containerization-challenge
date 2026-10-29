<template>
  <h1>Overview</h1>
  <p v-if="error" class="error">{{ error }}</p>
  <div v-if="summary" class="grid cards">
    <KpiCard label="Open shipments" :value="summary.openShipments" />
    <KpiCard label="Late or at risk" :value="summary.lateShipments" :bad="summary.lateShipments > 0" />
    <KpiCard label="On-time delivery 7 d" :value="summary.onTimeDeliveryPercent7d" unit="%" :bad="summary.onTimeDeliveryPercent7d < 90" />
    <KpiCard label="On-time pickup 7 d" :value="summary.onTimePickupPercent7d" unit="%" :bad="summary.onTimePickupPercent7d < 90" />
    <KpiCard label="Delivered today" :value="summary.deliveredToday" />
    <KpiCard label="Pickups due today" :value="summary.pickupsDueToday" />
    <KpiCard label="Critical alerts" :value="severity('Critical')" :bad="severity('Critical') > 0" />
    <KpiCard label="Vehicles reporting" :value="summary.vehiclesReporting" />
  </div>

  <div v-if="summary" class="grid two" style="margin-top: 12px">
    <div class="panel">
      <h2>Open shipments by status</h2>
      <ChartBox type="bar" :labels="statusLabels" :datasets="statusData" :height="240" :options="{ plugins: { legend: { display: false } } }" />
      <h2 style="margin-top: 16px">By transport mode</h2>
      <table>
        <tr v-for="m in summary.byMode" :key="m.key">
          <td>{{ m.key }}</td>
          <td style="text-align: right">{{ m.count }}</td>
        </tr>
      </table>
    </div>
    <div class="panel">
      <h2>Live events</h2>
      <p v-if="!live.events.length" class="muted">Waiting for events…</p>
      <ul class="feed">
        <li v-for="e in live.events" :key="e.id">
          <span class="muted">{{ e.at.toLocaleTimeString('en-GB') }}</span>
          <span :class="['pill', pillFor(e.kind)]">{{ e.kind.replace('alert-', '') }}</span>
          <router-link v-if="e.shipmentId" :to="'/shipments/' + e.shipmentId">{{ e.text }}</router-link>
          <span v-else>{{ e.text }}</span>
        </li>
      </ul>
    </div>
  </div>
  <p v-if="summary" class="muted">Updated {{ fmtDateTime(summary.generatedAt) }}</p>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import api from '../services/api'
import { fmtDateTime, statusText } from '../services/format'
import { useLiveStore } from '../stores/live'
import KpiCard from '../components/KpiCard.vue'
import ChartBox from '../components/ChartBox.vue'

const live = useLiveStore()
const summary = ref(null)
const error = ref(null)
let timer = null

const statusLabels = computed(() => summary.value.byStatus.map((s) => statusText[s.key] || s.key))
const statusData = computed(() => [{ label: 'Shipments', data: summary.value.byStatus.map((s) => s.count), backgroundColor: '#4fa3f7' }])

function severity(name) {
  const s = summary.value.openAlertsBySeverity.find((x) => x.key === name)
  return s ? s.count : 0
}

function pillFor(kind) {
  if (kind === 'alert-critical') return 'critical'
  if (kind === 'alert-warning') return 'warning'
  if (kind === 'milestone' || kind === 'closed') return 'ok'
  return 'info'
}

async function load() {
  try {
    summary.value = await api.summary()
    error.value = null
  } catch (e) {
    error.value = 'Could not load the summary: ' + e.message
  }
}

onMounted(() => {
  load()
  timer = setInterval(load, 60000)
})
onUnmounted(() => clearInterval(timer))
</script>
