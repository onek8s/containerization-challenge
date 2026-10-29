import Vue from 'vue'
import { BootstrapVue } from 'bootstrap-vue'
import Vuelidate from 'vuelidate'
import 'bootstrap/dist/css/bootstrap.css'
import 'bootstrap-vue/dist/bootstrap-vue.css'
import './site.css'
import App from './App.vue'
import router from './router'
import { eur, date, percent } from './format'

Vue.use(BootstrapVue)
Vue.use(Vuelidate)
Vue.filter('eur', eur)
Vue.filter('date', date)
Vue.filter('percent', percent)

new Vue({ router, render: h => h(App) }).$mount('#app')
