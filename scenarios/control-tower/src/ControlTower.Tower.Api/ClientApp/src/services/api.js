import axios from 'axios'
import { accessToken, ensureSignedIn } from './auth'

const http = axios.create({ baseURL: '/api', timeout: 30000 })

http.interceptors.request.use((cfg) => {
  const token = accessToken()
  if (token) cfg.headers.Authorization = 'Bearer ' + token
  return cfg
})

http.interceptors.response.use(
  (r) => r,
  async (err) => {
    if (err.response && err.response.status === 401) {
      await ensureSignedIn(window.location.pathname.replace(/^\/app/, ''))
    }
    return Promise.reject(err)
  }
)

export default {
  summary: () => http.get('/dashboard/summary').then((r) => r.data),
  map: () => http.get('/dashboard/map').then((r) => r.data),
  shipments: (params) => http.get('/shipments', { params }).then((r) => r.data),
  shipment: (id) => http.get('/shipments/' + id).then((r) => r.data),
  alerts: (params) => http.get('/alerts', { params }).then((r) => r.data),
  acknowledge: (id) => http.post('/alerts/' + id + '/acknowledge').then((r) => r.data),
  resolve: (id, resolution) => http.post('/alerts/' + id + '/resolve', { resolution }).then((r) => r.data),
  kpiDaily: (params) => http.get('/kpi/daily', { params }).then((r) => r.data),
  carriers: (days) => http.get('/kpi/carriers', { params: { days } }).then((r) => r.data),
  terminals: (hours) => http.get('/kpi/terminals', { params: { hours } }).then((r) => r.data)
}
