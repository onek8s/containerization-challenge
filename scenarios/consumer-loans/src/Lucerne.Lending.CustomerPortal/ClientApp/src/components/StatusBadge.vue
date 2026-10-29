<template>
  <b-badge :variant="variant" class="status-pill">{{ label }}</b-badge>
</template>

<script>
const labels = {
  Submitted: 'Received', Scoring: 'Processing', Referred: 'In review', Approved: 'Approved', Declined: 'Declined',
  OfferIssued: 'Offer ready', OfferAccepted: 'Offer accepted', AwaitingSignature: 'Waiting for signature', Signed: 'Signed',
  Booked: 'Opening loan', Disbursed: 'Paid out', Cancelled: 'Cancelled', Expired: 'Expired', Failed: 'Processing'
}
const variants = {
  Declined: 'danger', Cancelled: 'secondary', Expired: 'secondary', OfferIssued: 'success',
  AwaitingSignature: 'warning', Disbursed: 'success'
}

export default {
  props: { status: { type: String, required: true } },
  computed: {
    label () { return labels[this.status] || this.status },
    variant () { return variants[this.status] || 'info' }
  }
}
</script>
