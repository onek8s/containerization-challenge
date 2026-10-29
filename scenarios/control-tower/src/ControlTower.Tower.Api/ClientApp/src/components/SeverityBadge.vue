<template>
  <span :class="['pill', severity.toLowerCase()]">{{ severity }}</span>
</template>

<script setup>
defineProps({ severity: { type: String, required: true } })
</script>
