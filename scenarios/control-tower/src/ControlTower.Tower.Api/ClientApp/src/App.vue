<template>
  <div class="shell">
    <header class="topbar">
      <div class="brand">
        <span class="logo">PF</span>
        <span>Control Tower</span>
      </div>
      <nav>
        <router-link to="/" exact-active-class="active">Overview</router-link>
        <router-link to="/map">Live map</router-link>
        <router-link to="/shipments">Shipments</router-link>
        <router-link to="/alerts" @click="live.clearAlerts()">
          Alerts <span v-if="live.newAlerts" class="pill critical">{{ live.newAlerts }}</span>
        </router-link>
        <router-link to="/kpi">KPIs</router-link>
        <router-link to="/carriers">Carriers</router-link>
        <router-link to="/terminals">Terminals</router-link>
      </nav>
      <div class="user">
        <span :class="['dot', connected ? 'ok' : 'down']" :title="connected ? 'Live updates connected' : 'Live updates disconnected'"></span>
        <span>{{ name }}</span>
        <button v-if="!devSignIn" class="link" @click="logout">Sign out</button>
      </div>
    </header>
    <main>
      <router-view />
    </main>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { HubConnectionState } from '@microsoft/signalr'
import { displayName, signOut, appConfig } from './services/auth'
import { onState, liveConnection } from './services/live'
import { useLiveStore } from './stores/live'

const live = useLiveStore()
const state = ref(HubConnectionState.Disconnected)
const connected = computed(() => state.value === HubConnectionState.Connected)
const name = displayName()
const devSignIn = appConfig() && appConfig().developmentSignIn
let unsubscribe = null

onMounted(() => {
  unsubscribe = onState((s) => (state.value = s))
  liveConnection()
  live.start()
})
onUnmounted(() => unsubscribe && unsubscribe())

function logout() {
  signOut()
}
</script>
