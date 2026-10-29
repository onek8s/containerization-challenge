<template>
  <b-card>
    <h3>Loan application</h3>
    <b-nav pills class="my-3">
      <b-nav-item v-for="(s, i) in steps" :key="s" :active="step === i" :disabled="i > step" @click="step = i">{{ i + 1 }}. {{ s }}</b-nav-item>
    </b-nav>

    <div v-if="step === 0">
      <b-form-group label="Loan">
        <b-form-radio-group v-model="req.productCode" :options="productOptions" stacked @change="productChanged" />
      </b-form-group>
      <template v-if="productInfo">
        <b-form-row>
          <b-col md="4"><b-form-group :label="`Amount € (${productInfo.minAmount} - ${productInfo.maxAmount})`"><b-form-input v-model.number="req.amount" type="number" :min="productInfo.minAmount" :max="productInfo.maxAmount" step="100" /></b-form-group></b-col>
          <b-col md="4"><b-form-group :label="`Repayment period, months (${productInfo.minTermMonths} - ${productInfo.maxTermMonths})`"><b-form-input v-model.number="req.termMonths" type="number" :min="productInfo.minTermMonths" :max="productInfo.maxTermMonths" /></b-form-group></b-col>
          <b-col md="4"><b-form-group label="Due day of month"><b-form-select v-model="req.dueDay" :options="dueDays" /></b-form-group></b-col>
        </b-form-row>
        <b-form-group label="Purpose"><b-form-select v-model="req.purpose" :options="productInfo.purposes" /></b-form-group>
        <template v-if="productInfo.requiresCollateral">
          <b-form-group label="Vehicle (make, model, registration number)"><b-form-input v-model="req.collateralDescription" /></b-form-group>
          <b-form-group label="Purchase price €"><b-form-input v-model.number="req.collateralValue" type="number" min="0" /></b-form-group>
        </template>
        <b-form-checkbox v-if="productInfo.allowsCoApplicant" v-model="withCoApplicant">I am applying together with another person</b-form-checkbox>
      </template>
    </div>

    <div v-if="step === 1">
      <p class="text-muted">Your name and identity code come from your online service account.</p>
      <applicant-form v-model="req.applicant" locked />
    </div>

    <div v-if="step === 2 && withCoApplicant">
      <applicant-form v-model="req.coApplicant" />
    </div>

    <div v-if="step === steps.length - 1">
      <b-table-simple small class="mb-3">
        <b-tr><b-th>Loan</b-th><b-td>{{ productInfo && productInfo.name }}, {{ req.purpose }}</b-td></b-tr>
        <b-tr><b-th>Amount</b-th><b-td>{{ req.amount | eur }} over {{ req.termMonths }} months, due day {{ req.dueDay }}</b-td></b-tr>
        <b-tr v-if="estimate"><b-th>Estimated payment</b-th><b-td>{{ estimate.monthlyPayment + estimate.monthlyFee | eur }} / month, APR {{ estimate.aprPercent | percent }}</b-td></b-tr>
        <b-tr><b-th>Applicant</b-th><b-td>{{ req.applicant.firstName }} {{ req.applicant.lastName }}</b-td></b-tr>
        <b-tr v-if="withCoApplicant"><b-th>Co-applicant</b-th><b-td>{{ req.coApplicant.firstName }} {{ req.coApplicant.lastName }}</b-td></b-tr>
      </b-table-simple>
      <b-form-checkbox v-model="req.consentBureau">I consent to the bank checking my credit information and income details from the credit bureau and the positive credit register.</b-form-checkbox>
      <b-form-checkbox v-model="req.consentMarketing" class="mt-2">The bank may send me offers by e-mail.</b-form-checkbox>
    </div>

    <b-alert :show="!!error" variant="danger" class="mt-3">{{ error }}</b-alert>
    <div class="mt-4">
      <b-button v-if="step > 0" variant="outline-secondary" @click="step--">Back</b-button>
      <b-button v-if="step < steps.length - 1" variant="primary" class="ml-2" :disabled="!productInfo" @click="next">Next</b-button>
      <b-button v-else variant="primary" class="ml-2" :disabled="!req.consentBureau || busy" @click="submit">
        <b-spinner v-if="busy" small /> Send application
      </b-button>
    </div>
  </b-card>
</template>

<script>
import api, { problem } from '../api'
import ApplicantForm from '../components/ApplicantForm.vue'

function emptyApplicant () {
  return {
    personalIdentityCode: '', firstName: '', lastName: '', email: '', phone: '', streetAddress: '', postalCode: '', city: '',
    citizenship: 'FI', residentInFinland: true, maritalStatus: 'Single', housing: 'Rent', adults: 1, children: 0,
    employment: 'Permanent', employer: '', employedSince: null, onProbation: false, grossMonthlyIncome: 0, netMonthlyIncome: 0,
    housingCostMonthly: 0, otherLoanPaymentsMonthly: 0, otherLoanBalance: 0, politicallyExposed: false
  }
}

export default {
  components: { ApplicantForm },
  props: { product: String, me: Object },
  data () {
    return {
      products: [],
      step: 0,
      withCoApplicant: false,
      busy: false,
      error: null,
      estimate: null,
      req: {
        productCode: this.$route.params.product || null,
        purpose: 'Other',
        amount: Number(this.$route.query.amount) || 10000,
        termMonths: Number(this.$route.query.term) || 60,
        dueDay: 15,
        collateralDescription: null,
        collateralValue: null,
        consentBureau: false,
        consentMarketing: false,
        applicant: emptyApplicant(),
        coApplicant: null
      }
    }
  },
  computed: {
    productInfo () { return this.products.find(p => p.code === this.req.productCode) },
    productOptions () { return this.products.map(p => ({ value: p.code, text: `${p.name} (from ${p.indicativeRatePercent.toFixed(2)} %)` })) },
    steps () { return this.withCoApplicant ? ['Loan', 'Your details', 'Co-applicant', 'Send'] : ['Loan', 'Your details', 'Send'] },
    dueDays () { return [1, 5, 10, 15, 20, 25, 28] }
  },
  watch: {
    me: { immediate: true, handler: 'prefill' },
    withCoApplicant (v) { this.req.coApplicant = v ? emptyApplicant() : null }
  },
  async created () {
    this.products = await api.products()
    if (this.req.productCode) this.productChanged()
  },
  methods: {
    prefill (me) {
      if (!me) return
      Object.assign(this.req.applicant, { firstName: me.firstName, lastName: me.lastName, email: me.email, phone: me.phone || '' })
    },
    productChanged () {
      const p = this.productInfo
      if (!p) return
      if (!p.purposes.includes(this.req.purpose)) this.req.purpose = p.purposes[0]
      if (!p.allowsCoApplicant) this.withCoApplicant = false
    },
    async next () {
      this.error = null
      this.step++
      if (this.step === this.steps.length - 1) {
        try {
          this.estimate = await api.calculate({ productCode: this.req.productCode, amount: this.req.amount, termMonths: this.req.termMonths })
        } catch (e) {
          this.error = problem(e)
        }
      }
    },
    async submit () {
      this.busy = true
      this.error = null
      try {
        const created = await api.submit(this.req)
        this.$router.push('/applications/' + created.reference)
      } catch (e) {
        this.error = problem(e)
      } finally {
        this.busy = false
      }
    }
  }
}
</script>
