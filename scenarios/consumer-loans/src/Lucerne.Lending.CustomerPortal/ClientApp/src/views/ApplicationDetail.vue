<template>
  <div v-if="app">
    <b-card>
      <h3>{{ app.productName }} {{ app.amount | eur }} <status-badge :status="app.status" /></h3>
      <p class="text-muted">Application {{ app.reference }}, sent {{ app.createdUtc | date }}</p>
      <p>{{ app.statusText }}</p>
      <b-button v-if="app.signingUrl" :href="app.signingUrl" variant="primary">Sign the loan agreement</b-button>
      <p v-if="app.coreAccountNumber">Loan account {{ app.coreAccountNumber }}</p>
    </b-card>

    <b-card v-if="app.offer" title="Loan offer" class="mt-3">
      <b-table-simple small>
        <b-tr><b-th>Amount</b-th><b-td>{{ app.offer.amount | eur }}</b-td></b-tr>
        <b-tr><b-th>Repayment period</b-th><b-td>{{ app.offer.termMonths }} months</b-td></b-tr>
        <b-tr><b-th>Interest</b-th><b-td>{{ app.offer.referenceRatePercent | percent }} reference rate + {{ app.offer.marginPercent | percent }} margin = {{ app.offer.nominalRatePercent | percent }}</b-td></b-tr>
        <b-tr><b-th>APR</b-th><b-td>{{ app.offer.aprPercent | percent }}</b-td></b-tr>
        <b-tr><b-th>Monthly payment</b-th><b-td>{{ app.offer.monthlyPayment | eur }} + account fee {{ app.offer.monthlyFee | eur }}</b-td></b-tr>
        <b-tr><b-th>Arrangement fee</b-th><b-td>{{ app.offer.arrangementFee | eur }}</b-td></b-tr>
        <b-tr><b-th>Total repayable</b-th><b-td>{{ app.offer.totalRepayable | eur }}</b-td></b-tr>
        <b-tr><b-th>Valid until</b-th><b-td>{{ app.offer.validUntilUtc | date }}</b-td></b-tr>
      </b-table-simple>
      <template v-if="app.status === 'OfferIssued' && app.offer.status === 'Open'">
        <b-form-group label="Pay the loan to account (IBAN)" :state="ibanState" invalid-feedback="Check the account number">
          <b-form-input v-model="iban" :state="ibanState" placeholder="FI00 0000 0000 0000 00" @blur="iban = format(iban)" />
        </b-form-group>
        <b-form-checkbox v-model="confirmed">I have read the pre-contract information (SECCI) and the general loan terms.</b-form-checkbox>
        <b-button variant="primary" class="mt-3" :disabled="!confirmed || !ibanState || busy" @click="accept">Accept the offer</b-button>
      </template>
    </b-card>

    <b-card v-if="app.documents.length" title="Documents" class="mt-3">
      <b-list-group flush>
        <b-list-group-item v-for="d in app.documents" :key="d.id" :href="documentUrl(d.id)" target="_blank">
          {{ d.fileName }} <small class="text-muted">{{ d.createdUtc | date }}</small>
        </b-list-group-item>
      </b-list-group>
    </b-card>

    <b-alert :show="!!error" variant="danger" class="mt-3">{{ error }}</b-alert>
    <b-button v-if="app.canCancel" variant="outline-danger" class="mt-3" :disabled="busy" @click="cancel">Cancel the application</b-button>
  </div>
  <b-alert v-else-if="error" show variant="danger">{{ error }}</b-alert>
</template>

<script>
import api, { problem } from '../api'
import { ibanFormat, ibanValid } from '../format'
import StatusBadge from '../components/StatusBadge.vue'

export default {
  components: { StatusBadge },
  props: { reference: { type: String, required: true } },
  data: () => ({ app: null, iban: '', confirmed: false, busy: false, error: null, timer: null }),
  computed: {
    ibanState () { return this.iban ? ibanValid(this.iban) : null }
  },
  async created () {
    await this.load()
  },
  beforeDestroy () { clearTimeout(this.timer) },
  methods: {
    format: ibanFormat,
    documentUrl (id) { return api.documentUrl(this.reference, id) },
    async load () {
      try {
        this.app = await api.application(this.reference)
        if (this.app.disbursementIban && !this.iban) this.iban = ibanFormat(this.app.disbursementIban)
      } catch (e) {
        this.error = problem(e)
      }
      // processing steps run in the background; refresh while the application is moving
      if (this.app && ['Submitted', 'Scoring', 'Approved', 'OfferAccepted', 'Signed', 'Booked', 'Failed'].includes(this.app.status)) {
        this.timer = setTimeout(this.load, 5000)
      }
    },
    async accept () {
      this.busy = true
      this.error = null
      try {
        await api.accept(this.reference, { offerId: this.app.offer.id, disbursementIban: this.iban.replace(/\s+/g, ''), confirmsInformationRead: this.confirmed })
        await this.load()
      } catch (e) {
        this.error = problem(e)
      } finally {
        this.busy = false
      }
    },
    async cancel () {
      if (!(await this.$bvModal.msgBoxConfirm('Cancel this loan application?'))) return
      this.busy = true
      try {
        await api.cancel(this.reference)
        await this.load()
      } catch (e) {
        this.error = problem(e)
      } finally {
        this.busy = false
      }
    }
  }
}
</script>
