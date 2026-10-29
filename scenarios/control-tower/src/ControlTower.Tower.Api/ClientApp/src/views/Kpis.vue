<template>
  <h1>KPIs</h1>
  <div class="toolbar">
    <label>From <input type="date" v-model="from" /></label>
    <label>To <input type="date" v-model="to" /></label>
    <select v-model="groupBy">
      <option value="Total">Total</option>
      <option value="Mode">By mode</option>
      <option value="Customer">By customer</option>
    </select>
    <button class="primary" @click="load">Show</button>
  </div>
  <p v-if="error" class="error">{{ error }}</p>
  <p v-if="loaded && !points.length" class="muted">No KPI figures for the period. They are calculated every 15 minutes by the jobs service.</p>
  <div class="grid two" v-if="points.length">
    <div class="panel">
      <h2>On-time delivery %</h2>
      <ChartBox :labels="days" :datasets="series('onTimeDeliveryPercent')" :options="percentOptions" />
    </div>
    <div class="panel">
      <h2>Delivered shipments</h2>
      <ChartBox type="bar" :labels="days" :datasets="series('delivered', true)" :options="{ scales: { x: { stacked: true }, y: { stacked: true } } }" />
    </div>
    <div class="panel">
      <h2>On-time pickup %</h2>
      <ChartBox :labels="days" :datasets="series('onTimePickupPercent')" :options="percentOptions" />
    </div>
    <div class="panel">
      <h2>Alerts raised</h2>
      <ChartBox type="bar" :labels="days" :datasets="series('alerts', true)" :options="{ scales: { x: { stacked: true }, y: { stacked: true } } }" />
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../services/api'
import ChartBox from '../components/ChartBox.vue'

const palette = ['#0b5ea8', '#1e8a4c', '#c98200', '#8d4fc4', '#c42b2b', '#3d9ea0', '#7a5c3e', '#d65fa0', '#5d6b7a', '#9bbf3a', '#e07b39', '#2f3d8f']
const iso = (d) => d.toISOString().substring(0, 10)
const today = new Date()
const from = ref(iso(new Date(today.getTime() - 30 * 86400000)))
const to = ref(iso(today))
const groupBy = ref('Total')
const points = ref([])
const loaded = ref(false)
const error = ref(null)

const percentOptions = { scales: { y: { min: 0, max: 100 } } }
const days = computed(() => [...new Set(points.value.map((p) => p.day.substring(0, 10)))].sort())
const groups = computed(() => [...new Set(points.value.map((p) => p.group))].sort())

function series(field, bar) {
  return groups.value.map((g, i) => ({
    label: g,
    data: days.value.map((d) => {
      const p = points.value.find((x) => x.group === g && x.day.substring(0, 10) === d)
      return p ? p[field] : null
    }),
    borderColor: palette[i % palette.length],
    backgroundColor: palette[i % palette.length] + (bar ? 'cc' : '33'),
    spanGaps: true,
    tension: 0.2
  }))
}

async function load() {
  try {
    points.value = await api.kpiDaily({ from: from.value, to: to.value, groupBy: groupBy.value })
    error.value = null
  } catch (e) {
    error.value = 'Could not load KPIs: ' + e.message
  }
  loaded.value = true
}

onMounted(load)
</script>
