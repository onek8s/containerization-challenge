import { createRouter, createWebHistory } from 'vue-router'
import { ensureSignedIn } from './services/auth'
import Overview from './views/Overview.vue'

const routes = [
  { path: '/', name: 'overview', component: Overview },
  { path: '/map', name: 'map', component: () => import('./views/LiveMap.vue') },
  { path: '/shipments', name: 'shipments', component: () => import('./views/Shipments.vue') },
  { path: '/shipments/:id(\\d+)', name: 'shipment', component: () => import('./views/ShipmentDetail.vue'), props: true },
  { path: '/alerts', name: 'alerts', component: () => import('./views/Alerts.vue') },
  { path: '/kpi', name: 'kpi', component: () => import('./views/Kpis.vue') },
  { path: '/carriers', name: 'carriers', component: () => import('./views/Carriers.vue') },
  { path: '/terminals', name: 'terminals', component: () => import('./views/Terminals.vue') },
  { path: '/callback', name: 'callback', component: () => import('./views/Callback.vue'), meta: { anonymous: true } },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

const router = createRouter({
  history: createWebHistory('/app/'),
  routes,
  linkActiveClass: 'active'
})

router.beforeEach(async (to) => {
  if (to.meta.anonymous) return true
  return ensureSignedIn(to.fullPath)
})

export default router
