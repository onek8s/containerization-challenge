<template>
  <h1>Terminal workload</h1>
  <div class="toolbar">
    <select v-model.number="hours" @change="load">
      <option :value="12">Next 12 hours</option>
      <option :value="24">Next 24 hours</option>
      <option :value="48">Next 48 hours</option>
    </select>
    <span class="muted">Arrivals / departures per hour (terminal local time). Overdue work is shown in the current hour.</span>
  </div>
  <p v-if="error" class="error">{{ error }}</p>
  <div v-for="t in terminals" :key="t.terminalId" class="panel" style="margin-bottom: 12px; overflow-x: auto">
    <h2>{{ t.code }} – {{ t.name }} <span class="muted">({{ t.onSite }} shipments waiting on site)</span></h2>
    <table class="heat">
      <tr>
        <th></th>
        <th v-for="h in t.hours" :key="h.hour">{{ localHour(h.hour, t.timeZoneId) }}</th>
      </tr>
      <tr>
        <th>In</th>
        <td v-for="h in t.hours" :key="'a' + h.hour" class="cell" :style="heat(h.arrivals)">{{ h.arrivals || '' }}</td>
      </tr>
      <tr>
        <th>Out</th>
        <td v-for="h in t.hours" :key="'d' + h.hour" class="cell" :style="heat(h.departures)">{{ h.departures || '' }}</td>
      </tr>
      <tr>
        <th>Pcs</th>
        <td v-for="h in t.hours" :key="'p' + h.hour" class="cell muted">{{ h.packages || '' }}</td>
      </tr>
    </table>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../services/api'

// Location master data carries Windows time zone ids; map the ones used in the network.
const ianaZones = {
  'FLE Standard Time': 'Europe/Helsinki',
  'W. Europe Standard Time': 'Europe/Berlin',
  'Central European Standard Time': 'Europe/Warsaw',
  'GMT Standard Time': 'Europe/London'
}

const hours = ref(24)
const terminals = ref([])
const error = ref(null)

function localHour(utc, tz) {
  return new Date(utc).toLocaleTimeString('en-GB', { hour: '2-digit', timeZone: ianaZones[tz] || 'UTC' })
}

function heat(n) {
  if (!n) return {}
  const alpha = Math.min(0.85, 0.15 + n * 0.12)
  return { background: `rgba(11, 94, 168, ${alpha})`, color: alpha > 0.5 ? '#fff' : '#000' }
}

async function load() {
  try {
    terminals.value = await api.terminals(hours.value)
    error.value = null
  } catch (e) {
    error.value = 'Could not load terminal workload: ' + e.message
  }
}

onMounted(load)
</script>
