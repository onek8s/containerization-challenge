import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { initAuth } from './services/auth'
import 'leaflet/dist/leaflet.css'
import './style.css'

async function start() {
  await initAuth()
  createApp(App).use(createPinia()).use(router).mount('#app')
}

start().catch((err) => {
  document.getElementById('app').innerText = 'Control Tower could not start: ' + err.message
  console.error(err)
})
