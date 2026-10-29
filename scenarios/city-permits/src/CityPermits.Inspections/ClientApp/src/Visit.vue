<template>
  <div v-if="visit">
    <h2>{{ visit.stage }}: {{ visit.siteAddress }}</h2>
    <p>{{ visit.title }}<br />{{ visit.applicantName }} <a :href="'tel:' + visit.applicantPhone">{{ visit.applicantPhone }}</a></p>
    <div v-for="item in answers" :key="item.code" class="visit">
      <div>{{ item.text }} <strong v-if="item.mandatory">*</strong></div>
      <button :class="{ pass: item.passed === true }" @click="item.passed = true">Pass</button>
      <button :class="{ fail: item.passed === false }" @click="item.passed = false">Fail</button>
      <input v-model="item.note" placeholder="Note" />
    </div>
    <textarea v-model="notes" rows="3" style="width:100%" placeholder="Notes"></textarea>
    <p><button @click="submit(false)">Complete visit</button> <button @click="submit(true)">No access</button></p>
    <p v-if="message">{{ message }}</p>
  </div>
</template>
<script>
import { queue } from './queue'
export default {
  props: ['id'],
  data: () => ({ visit: null, answers: [], notes: '', started: new Date().toISOString(), message: '' }),
  created () {
    this.visit = JSON.parse(localStorage.getItem('insp.visits') || '[]').find(v => String(v.id) === String(this.id))
    if (this.visit) this.answers = this.visit.checklist.map(c => ({ ...c }))
  },
  methods: {
    async submit (noAccess) {
      if (!noAccess && this.answers.some(a => a.mandatory && a.passed === null)) { this.message = 'Answer all items marked *.'; return }
      const pos = await new Promise(resolve => navigator.geolocation
        ? navigator.geolocation.getCurrentPosition(p => resolve(p.coords), () => resolve(null), { timeout: 5000 }) : resolve(null))
      queue.add({
        clientMutationId: crypto.randomUUID ? crypto.randomUUID() : String(Date.now()),
        inspectionId: this.visit.id, startedUtc: this.started, completedUtc: new Date().toISOString(), noAccess, notes: this.notes,
        answers: this.answers.map(a => ({ code: a.code, passed: a.passed, note: a.note })),
        latitude: pos && pos.latitude, longitude: pos && pos.longitude
      })
      const sent = await queue.flush()
      this.message = sent ? 'Sent.' : 'Saved on this device; it will be sent when you are back online.'
      setTimeout(() => this.$router.push('/'), 1200)
    }
  }
}
</script>
