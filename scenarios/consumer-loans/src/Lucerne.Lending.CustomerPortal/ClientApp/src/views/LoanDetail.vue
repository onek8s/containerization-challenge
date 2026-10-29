<template>
  <div>
    <b-card v-if="loan" :title="loan.productName + ' ' + loan.accountNumber">
      <b-row>
        <b-col md="6">
          <b-table-simple small>
            <b-tr><b-th>Original amount</b-th><b-td>{{ loan.originalAmount | eur }}</b-td></b-tr>
            <b-tr><b-th>Balance</b-th><b-td>{{ loan.balance | eur }}</b-td></b-tr>
            <b-tr><b-th>Interest rate</b-th><b-td>{{ loan.nominalRatePercent | percent }}</b-td></b-tr>
          </b-table-simple>
        </b-col>
        <b-col md="6">
          <b-table-simple small>
            <b-tr><b-th>Next payment</b-th><b-td>{{ loan.nextInstalment | eur }} on {{ loan.nextDueDate | date }}</b-td></b-tr>
            <b-tr><b-th>Payment reference</b-th><b-td><code>{{ loan.paymentReference }}</code></b-td></b-tr>
            <b-tr v-if="loan.arrears > 0"><b-th>Overdue</b-th><b-td class="text-danger">{{ loan.arrears | eur }} ({{ loan.daysPastDue }} days)</b-td></b-tr>
          </b-table-simple>
        </b-col>
      </b-row>
      <b-alert v-if="loan.arrears > 0" show variant="warning">
        Please pay the overdue amount with the reference above. If you have difficulties paying, call customer service to agree on a payment plan.
      </b-alert>
    </b-card>
    <b-card title="Transactions" class="mt-3">
      <b-alert :show="!!error" variant="warning">{{ error }}</b-alert>
      <b-table :items="payments" :fields="fields" small responsive show-empty empty-text="No transactions.">
        <template #cell(bookingDate)="d">{{ d.value | date }}</template>
        <template #cell()="d">{{ typeof d.value === 'number' ? $options.filters.eur(d.value) : d.value }}</template>
      </b-table>
    </b-card>
  </div>
</template>

<script>
import api, { problem } from '../api'

export default {
  props: { account: { type: String, required: true } },
  data: () => ({
    loan: null,
    payments: [],
    error: null,
    fields: [
      { key: 'bookingDate', label: 'Date' }, { key: 'type', label: 'Type' }, { key: 'amount', label: 'Amount', tdClass: 'num' },
      { key: 'principal', label: 'Principal', tdClass: 'num' }, { key: 'interest', label: 'Interest', tdClass: 'num' },
      { key: 'fees', label: 'Fees', tdClass: 'num' }, { key: 'balanceAfter', label: 'Balance', tdClass: 'num' }
    ]
  }),
  async created () {
    try {
      const loans = await api.loans()
      this.loan = loans.find(l => l.accountNumber === this.account)
      this.payments = await api.payments(this.account)
    } catch (e) {
      this.error = problem(e)
    }
  }
}
</script>
