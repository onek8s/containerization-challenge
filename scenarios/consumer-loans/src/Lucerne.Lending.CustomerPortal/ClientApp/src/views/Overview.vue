<template>
  <div>
    <h2 v-if="me">Hello {{ me.firstName }}</h2>
    <b-row class="mt-3">
      <b-col md="6">
        <b-card title="Open applications" class="mb-3">
          <p v-if="!open.length" class="text-muted">You have no open loan applications.</p>
          <b-list-group v-else flush>
            <b-list-group-item v-for="a in open" :key="a.reference" :to="'/applications/' + a.reference">
              {{ a.productName }} {{ a.amount | eur }} <status-badge :status="a.status" class="float-right" />
            </b-list-group-item>
          </b-list-group>
          <b-button to="/apply" variant="primary" class="mt-3">Apply for a loan</b-button>
        </b-card>
      </b-col>
      <b-col md="6">
        <b-card title="My loans" class="mb-3">
          <b-alert v-if="loanError" show variant="warning">{{ loanError }}</b-alert>
          <p v-else-if="!loans.length" class="text-muted">No loans with us.</p>
          <b-list-group v-else flush>
            <b-list-group-item v-for="l in loans" :key="l.accountNumber" :to="'/loans/' + l.accountNumber">
              {{ l.productName }}, balance {{ l.balance | eur }}
              <div class="small text-muted">Next payment {{ l.nextInstalment | eur }} on {{ l.nextDueDate | date }}</div>
              <b-badge v-if="l.arrears > 0" variant="danger">Overdue {{ l.arrears | eur }}</b-badge>
            </b-list-group-item>
          </b-list-group>
        </b-card>
      </b-col>
    </b-row>
    <b-alert v-if="me && !me.twoFactorEnabled" show variant="info">
      Protect your account: <a href="/account/manage/two-factor">set up sign-in confirmation with an authenticator app</a>.
    </b-alert>
  </div>
</template>

<script>
import api, { problem } from '../api'
import StatusBadge from '../components/StatusBadge.vue'

const closed = ['Declined', 'Cancelled', 'Expired', 'Disbursed']

export default {
  components: { StatusBadge },
  props: { me: Object },
  data: () => ({ applications: [], loans: [], loanError: null }),
  computed: {
    open () { return this.applications.filter(a => !closed.includes(a.status)) }
  },
  async created () {
    this.applications = await api.applications()
    try {
      this.loans = await api.loans()
    } catch (e) {
      this.loanError = problem(e)
    }
  }
}
</script>
