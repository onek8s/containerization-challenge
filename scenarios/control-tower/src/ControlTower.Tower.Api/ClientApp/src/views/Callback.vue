<template>
  <p class="muted">Signing in…</p>
  <p v-if="error" class="error">{{ error }}</p>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { completeSignIn } from '../services/auth'

const router = useRouter()
const error = ref(null)

onMounted(async () => {
  try {
    const path = await completeSignIn()
    router.replace(path)
  } catch (e) {
    error.value = 'Sign-in failed: ' + e.message
  }
})
</script>
