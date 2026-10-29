<template>
  <div>
    <b-form-row>
      <b-col md="6"><b-form-group label="First name"><b-form-input v-model="value.firstName" :disabled="locked" required /></b-form-group></b-col>
      <b-col md="6"><b-form-group label="Last name"><b-form-input v-model="value.lastName" :disabled="locked" required /></b-form-group></b-col>
    </b-form-row>
    <b-form-row v-if="!locked">
      <b-col md="6">
        <b-form-group label="Personal identity code" :state="picState" invalid-feedback="Check the identity code">
          <b-form-input v-model="value.personalIdentityCode" placeholder="DDMMYY-NNNC" :state="picState" />
        </b-form-group>
      </b-col>
    </b-form-row>
    <b-form-row>
      <b-col md="6"><b-form-group label="E-mail"><b-form-input v-model="value.email" type="email" /></b-form-group></b-col>
      <b-col md="6"><b-form-group label="Mobile phone"><b-form-input v-model="value.phone" type="tel" /></b-form-group></b-col>
    </b-form-row>
    <b-form-row>
      <b-col md="6"><b-form-group label="Street address"><b-form-input v-model="value.streetAddress" /></b-form-group></b-col>
      <b-col md="2"><b-form-group label="Postal code"><b-form-input v-model="value.postalCode" maxlength="5" /></b-form-group></b-col>
      <b-col md="4"><b-form-group label="City"><b-form-input v-model="value.city" /></b-form-group></b-col>
    </b-form-row>
    <b-form-row>
      <b-col md="4"><b-form-group label="Citizenship"><b-form-select v-model="value.citizenship" :options="countries" /></b-form-group></b-col>
      <b-col md="4"><b-form-group label="Marital status"><b-form-select v-model="value.maritalStatus" :options="marital" /></b-form-group></b-col>
      <b-col md="4"><b-form-group label="Housing"><b-form-select v-model="value.housing" :options="housing" /></b-form-group></b-col>
    </b-form-row>
    <b-form-row>
      <b-col md="3"><b-form-group label="Adults in household"><b-form-input v-model.number="value.adults" type="number" min="1" max="9" /></b-form-group></b-col>
      <b-col md="3"><b-form-group label="Children under 18"><b-form-input v-model.number="value.children" type="number" min="0" max="12" /></b-form-group></b-col>
      <b-col md="6"><b-form-group label="Housing costs € / month (rent, maintenance charge)"><b-form-input v-model.number="value.housingCostMonthly" type="number" min="0" /></b-form-group></b-col>
    </b-form-row>
    <h5 class="mt-3">Work and income</h5>
    <b-form-row>
      <b-col md="4"><b-form-group label="Employment"><b-form-select v-model="value.employment" :options="employment" /></b-form-group></b-col>
      <b-col md="4"><b-form-group label="Employer"><b-form-input v-model="value.employer" /></b-form-group></b-col>
      <b-col md="4"><b-form-group label="Employed since"><b-form-input v-model="value.employedSince" type="date" /></b-form-group></b-col>
    </b-form-row>
    <b-form-checkbox v-model="value.onProbation" class="mb-3">I am on a trial period</b-form-checkbox>
    <b-form-row>
      <b-col md="6"><b-form-group label="Gross income € / month"><b-form-input v-model.number="value.grossMonthlyIncome" type="number" min="0" /></b-form-group></b-col>
      <b-col md="6"><b-form-group label="Net income € / month"><b-form-input v-model.number="value.netMonthlyIncome" type="number" min="0" /></b-form-group></b-col>
    </b-form-row>
    <b-form-row>
      <b-col md="6"><b-form-group label="Other loans, total balance €"><b-form-input v-model.number="value.otherLoanBalance" type="number" min="0" /></b-form-group></b-col>
      <b-col md="6"><b-form-group label="Other loans, payments € / month"><b-form-input v-model.number="value.otherLoanPaymentsMonthly" type="number" min="0" /></b-form-group></b-col>
    </b-form-row>
    <b-form-checkbox v-model="value.politicallyExposed">I am a politically exposed person or a family member or close associate of one</b-form-checkbox>
  </div>
</template>

<script>
export default {
  props: { value: { type: Object, required: true }, locked: Boolean },
  data: () => ({
    countries: [{ value: 'FI', text: 'Finland' }, { value: 'SE', text: 'Sweden' }, { value: 'EE', text: 'Estonia' }, { value: 'XX', text: 'Other' }],
    marital: ['Single', 'Married', 'Cohabiting', 'Divorced', 'Widowed'],
    housing: [
      { value: 'Owner', text: 'Own home' }, { value: 'Rent', text: 'Rented' }, { value: 'RightOfOccupancy', text: 'Right of occupancy' },
      { value: 'WithParents', text: 'With parents' }, { value: 'Other', text: 'Other' }
    ],
    employment: [
      { value: 'Permanent', text: 'Permanent' }, { value: 'FixedTerm', text: 'Fixed term' }, { value: 'SelfEmployed', text: 'Self-employed' },
      { value: 'Pensioner', text: 'Pensioner' }, { value: 'Student', text: 'Student' }, { value: 'Unemployed', text: 'Unemployed' }, { value: 'Other', text: 'Other' }
    ]
  }),
  computed: {
    picState () {
      const v = (this.value.personalIdentityCode || '').trim()
      if (!v) return null
      return /^\d{6}[-+ABCDEFYXWVU]\d{3}[0-9A-Y]$/i.test(v)
    }
  }
}
</script>
