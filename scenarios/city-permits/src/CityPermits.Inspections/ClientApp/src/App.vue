<template>
  <div class="app">
    <header><strong>Inspections</strong> <span v-if="pending">· {{ pending }} waiting to send</span></header>
    <div v-if="needLogin" class="login">
      <p>Development sign-in</p>
      <select v-model="user"><option>CORP\p.doyle</option><option>CORP\l.chandler</option><option>CORP\t.nwosu</option></select>
      <button @click="login">Sign in</button>
    </div>
    <router-view v-else />
  </div>
</template>
<script>
import { auth } from './auth'
import { queue } from './queue'
export default {
  data: () => ({ user: 'CORP\\p.doyle', needLogin: auth.config.devMode && !auth.token, pending: queue.items().length }),
  methods: { async login () { await auth.devLogin(this.user); this.needLogin = false } },
  mounted () { setInterval(() => { this.pending = queue.items().length }, 2000) }
}
</script>
<style>
body { margin: 0; font-family: Arial, sans-serif; } header { background: #1d3557; color: #fff; padding: 12px; }
.app > div { padding: 12px; } .visit { border-bottom: 1px solid #ddd; padding: 8px 0; } button { padding: 10px 14px; font-size: 16px; }
.pass { background: #00703c; color: #fff; } .fail { background: #d4351c; color: #fff; }
</style>
