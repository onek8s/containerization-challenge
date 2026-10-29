<template>
  <b-card title="My loans">
    <b-alert :show="!!error" variant="warning">{{ error }}</b-alert>
    <b-table :items="items" :fields="fields" :busy="busy" show-empty empty-text="No loans with us." hover responsive @row-clicked="open">
      <template #cell(balance)="d">{{ d.value | eur }}</template>
      <template #cell(nextDueDate)="d">{{ d.value | date }} {{ d.item.nextInstalment | eur }}</template>
      <template #cell(arrears)="d"><b-badge v-if="d.value > 0" variant="danger">{{ d.value | eur }}</b-badge></template>
    </b-table>
  </b-card>
</template>

<script>
import api, { problem } from '../api'

export default {
  data: () => ({
    items: [],
    busy: true,
    error: null,
    fields: [
      { key: 'accountNumber', label: 'Loan' }, { key: 'productName', label: 'Type' }, { key: 'balance', label: 'Balance', tdClass: 'num' },
      { key: 'nextDueDate', label: 'Next payment' }, { key: 'arrears', label: 'Overdue' }
    ]
  }),
  async created () {
    try {
      this.items = await api.loans()
    } catch (e) {
      this.error = problem(e)
    } finally {
      this.busy = false
    }
  },
  methods: {
    open (row) { this.$router.push('/loans/' + row.accountNumber) }
  }
}
</script>
