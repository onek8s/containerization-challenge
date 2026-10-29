<template>
  <b-card title="My applications">
    <b-table :items="items" :fields="fields" :busy="busy" show-empty empty-text="No applications yet." hover responsive @row-clicked="open">
      <template #cell(amount)="d"><span class="num">{{ d.value | eur }}</span></template>
      <template #cell(createdUtc)="d">{{ d.value | date }}</template>
      <template #cell(status)="d"><status-badge :status="d.value" /></template>
    </b-table>
  </b-card>
</template>

<script>
import api from '../api'
import StatusBadge from '../components/StatusBadge.vue'

export default {
  components: { StatusBadge },
  data: () => ({
    items: [],
    busy: true,
    fields: [
      { key: 'reference', label: 'Application' }, { key: 'productName', label: 'Loan' },
      { key: 'amount', label: 'Amount', tdClass: 'num' }, { key: 'termMonths', label: 'Months' },
      { key: 'createdUtc', label: 'Sent' }, { key: 'status', label: 'Status' }
    ]
  }),
  async created () {
    this.items = await api.applications()
    this.busy = false
  },
  methods: {
    open (row) { this.$router.push('/applications/' + row.reference) }
  }
}
</script>
