import Vue from 'vue'
import VueRouter from 'vue-router'
import Overview from './views/Overview.vue'
import Calculator from './views/Calculator.vue'
import Apply from './views/Apply.vue'
import Applications from './views/Applications.vue'
import ApplicationDetail from './views/ApplicationDetail.vue'
import Loans from './views/Loans.vue'
import LoanDetail from './views/LoanDetail.vue'

Vue.use(VueRouter)

export default new VueRouter({
  mode: 'history',
  base: '/',
  routes: [
    { path: '/', component: Overview },
    { path: '/calculator', component: Calculator },
    { path: '/apply/:product?', component: Apply, props: true },
    { path: '/applications', component: Applications },
    { path: '/applications/:reference', component: ApplicationDetail, props: true },
    { path: '/loans', component: Loans },
    { path: '/loans/:account', component: LoanDetail, props: true },
    { path: '*', redirect: '/' }
  ],
  scrollBehavior: () => ({ x: 0, y: 0 })
})
