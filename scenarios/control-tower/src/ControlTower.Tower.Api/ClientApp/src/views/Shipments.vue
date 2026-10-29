<template>
  <h1>Shipments</h1>
  <form class="toolbar" @submit.prevent="search(1)">
    <input v-model="filter.q" placeholder="Number, reference, SSCC, carrier ref" size="32" />
    <select v-model="filter.status">
      <option value="">All statuses</option>
      <option v-for="(t, k) in statusText" :key="k" :value="k">{{ t }}</option>
    </select>
    <select v-model="filter.mode">
      <option value="">All modes</option>
      <option>Road</option><option>Sea</option><option>Air</option><option>Rail</option>
    </select>
    <label><input type="checkbox" v-model="filter.openOnly" /> Open only</label>
    <button class="primary" type="submit">Search</button>
    <span class="muted" v-if="result">{{ result.total }} shipments</span>
  </form>
  <p v-if="error" class="error">{{ error }}</p>
  <div class="panel" v-if="result">
    <table>
      <thead>
        <tr>
          <th>Number</th><th>Customer</th><th>Reference</th><th>Mode</th><th>Status</th>
          <th>From</th><th>To</th><th>Pickup</th><th>Delivery</th><th>ETA</th><th>Alerts</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="s in result.items" :key="s.id" class="clickable" @click="$router.push('/shipments/' + s.id)">
          <td><b>{{ s.number }}</b></td>
          <td>{{ s.customer }}</td>
          <td>{{ s.customerReference }}</td>
          <td>{{ s.mode }}</td>
          <td><StatusBadge :status="s.status" /></td>
          <td>{{ s.origin }}</td>
          <td>{{ s.destination }}</td>
          <td>{{ fmtDateTime(s.plannedPickup) }}</td>
          <td>{{ fmtDateTime(s.plannedDelivery) }}</td>
          <td :class="{ late: isLate(s) }">{{ fmtDateTime(s.eta) }}</td>
          <td><span v-if="s.openAlerts" class="pill warning">{{ s.openAlerts }}</span></td>
        </tr>
      </tbody>
    </table>
    <div class="toolbar" style="margin-top: 8px">
      <button :disabled="result.page <= 1" @click="search(result.page - 1)">Previous</button>
      <span>Page {{ result.page }} / {{ result.pages || 1 }}</span>
      <button :disabled="result.page >= result.pages" @click="search(result.page + 1)">Next</button>
    </div>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import api from '../services/api'
import { fmtDateTime, statusText } from '../services/format'
import StatusBadge from '../components/StatusBadge.vue'

const filter = reactive({ q: '', status: '', mode: '', openOnly: true })
const result = ref(null)
const error = ref(null)

function isLate(s) {
  return s.eta && new Date(s.eta) - new Date(s.plannedDelivery) > 2 * 3600000
}

async function search(page) {
  try {
    result.value = await api.shipments({
      q: filter.q || undefined,
      status: filter.status || undefined,
      mode: filter.mode || undefined,
      openOnly: filter.openOnly,
      page,
      pageSize: 50
    })
    error.value = null
  } catch (e) {
    error.value = 'Search failed: ' + e.message
  }
}

onMounted(() => search(1))
</script>
