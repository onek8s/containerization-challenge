<template>
  <span :class="['pill', cls]">{{ text }}</span>
</template>

<script setup>
import { computed } from 'vue'
import { statusText } from '../services/format'

const props = defineProps({ status: { type: String, required: true } })
const text = computed(() => statusText[props.status] || props.status)
const cls = computed(() => {
  switch (props.status) {
    case 'Delivered': return 'ok'
    case 'CustomsHold': return 'critical'
    case 'Cancelled': return ''
    case 'Booked':
    case 'Requested': return 'info'
    default: return 'warning'
  }
})
</script>
