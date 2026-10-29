<template>
  <div class="toolbar">
    <h1 style="margin: 0">Live map</h1>
    <label><input type="checkbox" v-model="showLegs" /> Open legs</label>
    <label><input type="checkbox" v-model="showStale" /> Vehicles without recent position</label>
    <span class="muted">{{ vehicleCount }} vehicles · {{ legCount }} legs · updated {{ updated }}</span>
  </div>
  <p v-if="error" class="error">{{ error }}</p>
  <div ref="mapEl" class="map"></div>
</template>

<script setup>
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import L from 'leaflet'
import api from '../services/api'
import { on, off } from '../services/live'
import { appConfig } from '../services/auth'
import { fmtDateTime, relative } from '../services/format'

const mapEl = ref(null)
const error = ref(null)
const showLegs = ref(true)
const showStale = ref(false)
const vehicleCount = ref(0)
const legCount = ref(0)
const updated = ref('')

let map = null
const vehicleLayer = L.layerGroup()
const legLayer = L.layerGroup()
const locationLayer = L.layerGroup()
const markers = new Map()
let data = null
let timer = null

function vehicleColor(v) {
  if (v.temperatureOutOfRange) return '#c42b2b'
  if (v.stale) return '#8a96a3'
  if (v.reefer) return '#1f8fd6'
  return '#1e8a4c'
}

function vehicleIcon(v) {
  return L.divIcon({
    className: '',
    iconSize: [18, 18],
    html: `<div style="width:16px;height:16px;border-radius:50%;border:2px solid #fff;box-shadow:0 0 3px #000;background:${vehicleColor(v)}"></div>`
  })
}

function vehiclePopup(v) {
  const temp = v.temperature !== null && v.temperature !== undefined ? `<br/>Reefer ${v.temperature} °C` : ''
  const shipment = v.shipmentNumber ? `<br/>Shipment ${v.shipmentNumber}` : ''
  return `<b>${v.plate}</b> (${v.carrier || ''})<br/>${v.speedKmh ?? 0} km/h, ${relative(v.lastPositionAt)}${temp}${shipment}`
}

function drawVehicles() {
  vehicleLayer.clearLayers()
  markers.clear()
  let count = 0
  for (const v of data.vehicles) {
    if (v.latitude === null || v.latitude === undefined) continue
    if (v.stale && !showStale.value) continue
    const m = L.marker([v.latitude, v.longitude], { icon: vehicleIcon(v), title: v.plate }).bindPopup(vehiclePopup(v))
    m.addTo(vehicleLayer)
    markers.set(v.id, { marker: m, vehicle: v })
    count++
  }
  vehicleCount.value = count
}

function drawLegs() {
  legLayer.clearLayers()
  if (!showLegs.value) { legCount.value = 0; return }
  for (const l of data.legs) {
    const color = l.late ? '#c42b2b' : l.mode === 'Sea' ? '#3d6f9e' : l.mode === 'Air' ? '#8d4fc4' : '#1e8a4c'
    L.polyline([[l.fromLatitude, l.fromLongitude], [l.toLatitude, l.toLongitude]], {
      color, weight: 2, opacity: 0.7, dashArray: l.status === 'Departed' ? null : '6 6'
    }).bindPopup(`<a href="/app/shipments/${l.shipmentId}">${l.shipmentNumber}</a> · ${l.mode}<br/>${l.from} → ${l.to}<br/>${l.carrier || ''} ${l.vehicle || ''}<br/>Planned ${fmtDateTime(l.plannedArrival)}${l.eta ? ', ETA ' + fmtDateTime(l.eta) : ''}`)
      .addTo(legLayer)
  }
  legCount.value = data.legs.length
}

function drawLocations() {
  locationLayer.clearLayers()
  for (const loc of data.locations) {
    L.circleMarker([loc.latitude, loc.longitude], {
      radius: loc.kind === 'Terminal' ? 7 : 5,
      color: '#13263b',
      fillColor: loc.kind === 'Terminal' ? '#f2b705' : '#ffffff',
      fillOpacity: 1,
      weight: 2
    }).bindTooltip(`${loc.code} – ${loc.name}`).addTo(locationLayer)
  }
}

async function load() {
  try {
    data = await api.map()
    drawLocations()
    drawLegs()
    drawVehicles()
    updated.value = new Date().toLocaleTimeString('en-GB')
  } catch (e) {
    error.value = 'Could not load map data: ' + e.message
  }
}

function onPosition(p) {
  const entry = markers.get(p.vehicleId)
  if (entry) {
    entry.marker.setLatLng([p.latitude, p.longitude])
    Object.assign(entry.vehicle, { latitude: p.latitude, longitude: p.longitude, speedKmh: p.speedKmh, temperature: p.temperature, lastPositionAt: p.occurredAt, stale: false })
    entry.marker.setPopupContent(vehiclePopup(entry.vehicle))
  } else if (data) {
    const v = data.vehicles.find((x) => x.id === p.vehicleId)
    if (v) {
      Object.assign(v, { latitude: p.latitude, longitude: p.longitude, speedKmh: p.speedKmh, temperature: p.temperature, lastPositionAt: p.occurredAt, stale: false })
      drawVehicles()
    }
  }
}

onMounted(async () => {
  const cfg = appConfig()
  map = L.map(mapEl.value, { preferCanvas: true }).setView([60.2, 22.5], 5)
  L.tileLayer(cfg.mapTileUrl, { maxZoom: 18, attribution: '&copy; OpenStreetMap contributors' }).addTo(map)
  locationLayer.addTo(map)
  legLayer.addTo(map)
  vehicleLayer.addTo(map)
  await load()
  on('VehiclePosition', onPosition)
  timer = setInterval(load, 120000)
})

watch(showLegs, () => data && drawLegs())
watch(showStale, () => data && drawVehicles())

onBeforeUnmount(() => {
  off('VehiclePosition', onPosition)
  clearInterval(timer)
  if (map) map.remove()
})
</script>
