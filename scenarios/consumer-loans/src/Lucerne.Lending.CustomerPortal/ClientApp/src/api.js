import axios from 'axios'

const http = axios.create({
  baseURL: '/portal-api',
  headers: { 'X-Portal-Request': '1' },
  timeout: 45000
})

http.interceptors.response.use(r => r, error => {
  if (error.response && error.response.status === 401) {
    window.location.href = '/account/login?returnUrl=' + encodeURIComponent(window.location.pathname)
  }
  return Promise.reject(error)
})

export function problem (error) {
  const r = error && error.response
  if (!r) return 'The service could not be reached. Please check your connection.'
  const d = r.data || {}
  if (d.errors) return Object.values(d.errors).flat().join(' ')
  return d.detail || d.title || `Request failed (${r.status}).`
}

export default {
  config: () => http.get('/config').then(r => r.data),
  me: () => http.get('/me').then(r => r.data),
  products: () => http.get('/products').then(r => r.data),
  calculate: req => http.post('/calculate', req).then(r => r.data),
  submit: req => http.post('/applications', req).then(r => r.data),
  applications: () => http.get('/applications').then(r => r.data),
  application: ref => http.get(`/applications/${encodeURIComponent(ref)}`).then(r => r.data),
  accept: (ref, req) => http.post(`/applications/${encodeURIComponent(ref)}/accept`, req),
  cancel: ref => http.post(`/applications/${encodeURIComponent(ref)}/cancel`),
  documentUrl: (ref, id) => `/portal-api/applications/${encodeURIComponent(ref)}/documents/${id}`,
  loans: () => http.get('/loans').then(r => r.data),
  payments: account => http.get(`/loans/${encodeURIComponent(account)}/payments`).then(r => r.data)
}
