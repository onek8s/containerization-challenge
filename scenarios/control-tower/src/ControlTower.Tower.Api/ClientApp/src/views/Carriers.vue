<template>
  <h1>Carrier scorecard</h1>
  <div class="toolbar">
    <select v-model.number="days" @change="load">
      <option :value="7">Last 7 days</option>
      <option :value="30">Last 30 days</option>
      <option :value="90">Last 90 days</option>
    </select>
  </div>
  <p v-if="error" class="error">{{ error }}</p>
  <div class="panel">
    <table>
      <thead>
        <tr><th>Carrier</th><th>Mode</th><th>Integration</th><th>Legs</th><th>Arrived</th><th>On time</th><th>Target</th><th>Avg delay</th><th>Open</th><th>Open late</th></tr>
      </thead>
      <tbody>
        <tr v-for="c in rows" :key="c.carrierId">
          <td><b>{{ c.code }}</b> {{ c.name }}</td>
          <td>{{ c.mode }}</td>
          <td>{{ integration[c.integration] || c.integration }}</td>
          <td>{{ c.legs }}</td>
          <td>{{ c.arrivedLegs }}</td>
          <td :class="{ late: c.onTimePercent < c.targetPercent }">{{ fmtNumber(c.onTimePercent, 1) }} %</td>
          <td>{{ fmtNumber(c.targetPercent, 0) }} %</td>
          <td>{{ fmtNumber(c.averageDelayHours, 1) }} h</td>
          <td>{{ c.openLegs }}</td>
          <td><span v-if="c.lateOpenLegs" class="pill warning">{{ c.lateOpenLegs }}</span></td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../services/api'
import { fmtNumber } from '../services/format'

const integration = { Manual: 'Manual', EdifactSftp: 'EDIFACT / SFTP', RestApi: 'REST API' }
const days = ref(30)
const rows = ref([])
const error = ref(null)

async function load() {
  try {
    rows.value = await api.carriers(days.value)
    error.value = null
  } catch (e) {
    error.value = 'Could not load the scorecard: ' + e.message
  }
}

onMounted(load)
</script>
