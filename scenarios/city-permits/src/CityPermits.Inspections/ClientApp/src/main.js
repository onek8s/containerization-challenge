import Vue from 'vue'
import VueRouter from 'vue-router'
import 'leaflet/dist/leaflet.css'
import App from './App.vue'
import Visits from './Visits.vue'
import Visit from './Visit.vue'
import { auth } from './auth'

Vue.use(VueRouter)
const router = new VueRouter({
  mode: 'history',
  routes: [
    { path: '/', component: Visits },
    { path: '/visit/:id', component: Visit, props: true }
  ]
})

auth.init().then(() => new Vue({ router, render: h => h(App) }).$mount('#app'))
