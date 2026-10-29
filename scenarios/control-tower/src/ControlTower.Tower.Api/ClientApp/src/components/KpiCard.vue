<template>
  <div class="panel card">
    <div class="label">{{ label }}</div>
    <div class="value" :class="{ late: bad }">{{ value }}<small v-if="unit"> {{ unit }}</small></div>
    <div v-if="hint" class="muted">{{ hint }}</div>
  </div>
</template>

<script setup>
defineProps({
  label: String,
  value: [String, Number],
  unit: String,
  hint: String,
  bad: Boolean
})
</script>
