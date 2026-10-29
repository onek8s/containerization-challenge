<template>
  <div>
    <input type="date" v-model="date" @change="load" />
    <p v-if="error">{{ error }}</p>
    <div v-for="v in visits" :key="v.id" class="visit">
      <router-link :to="'/visit/' + v.id">{{ v.routeOrder || '-' }}. {{ v.siteAddress }}</router-link>
      <div>{{ v.stage }} · {{ v.caseReference }} · {{ v.slot }} · <em>{{ v.status }}</em></div>
    </div>
    <p v-if="!visits.length && !error">No visits.</p>
  </div>
</template>
<script>
import { auth } from './auth'
export default {
  data: () => ({ date: new Date().toISOString().substring(0, 10), visits: [], error: null }),
  methods: {
    async load () {
      try {
        this.visits = (await auth.http().get('/api/visits', { params: { date: this.date } })).data
        localStorage.setItem('insp.visits', JSON.stringify(this.visits))
        this.error = null
      } catch (e) {
        this.visits = JSON.parse(localStorage.getItem('insp.visits') || '[]')
        this.error = 'Offline: showing the last downloaded list.'
      }
    }
  },
  mounted () { this.load() }
}
</script>
