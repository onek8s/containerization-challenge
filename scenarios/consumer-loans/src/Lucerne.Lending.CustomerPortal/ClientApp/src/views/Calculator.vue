<template>
  <b-card title="Loan calculator">
    <b-form-group label="Loan">
      <b-form-select v-model="productCode" :options="productOptions" @change="resetLimits" />
    </b-form-group>
    <template v-if="product">
      <b-form-group :label="'Amount ' + $options.filters.eur(amount)">
        <b-form-input v-model.number="amount" type="range" :min="product.minAmount" :max="product.maxAmount" step="500" />
      </b-form-group>
      <b-form-group :label="'Repayment period ' + term + ' months'">
        <b-form-input v-model.number="term" type="range" :min="product.minTermMonths" :max="product.maxTermMonths" step="6" />
      </b-form-group>
      <b-overlay :show="busy">
        <b-card v-if="result" bg-variant="light">
          <h3>{{ result.monthlyPayment + result.monthlyFee | eur }} / month</h3>
          <p class="mb-1">Nominal rate {{ result.nominalRatePercent | percent }}, APR {{ result.aprPercent | percent }}</p>
          <p class="small text-muted">{{ result.example }}</p>
        </b-card>
      </b-overlay>
      <b-alert :show="!!error" variant="danger" class="mt-2">{{ error }}</b-alert>
      <b-button variant="primary" class="mt-3" :to="{ path: '/apply/' + productCode, query: { amount, term } }">Apply for this loan</b-button>
    </template>
  </b-card>
</template>

<script>
import api, { problem } from '../api'

let timer = null

export default {
  data: () => ({ products: [], productCode: null, amount: 10000, term: 60, result: null, busy: false, error: null }),
  computed: {
    product () { return this.products.find(p => p.code === this.productCode) },
    productOptions () { return this.products.map(p => ({ value: p.code, text: p.name })) }
  },
  watch: {
    amount () { this.schedule() },
    term () { this.schedule() },
    productCode () { this.schedule() }
  },
  async created () {
    this.products = await api.products()
    if (this.products.length) this.productCode = this.products[0].code
  },
  methods: {
    resetLimits () {
      const p = this.product
      this.amount = Math.min(Math.max(this.amount, p.minAmount), p.maxAmount)
      this.term = Math.min(Math.max(this.term, p.minTermMonths), p.maxTermMonths)
    },
    schedule () {
      clearTimeout(timer)
      timer = setTimeout(this.calculate, 300)
    },
    async calculate () {
      if (!this.product) return
      this.busy = true
      this.error = null
      try {
        this.result = await api.calculate({ productCode: this.productCode, amount: this.amount, termMonths: this.term })
      } catch (e) {
        this.error = problem(e)
      } finally {
        this.busy = false
      }
    }
  }
}
</script>
