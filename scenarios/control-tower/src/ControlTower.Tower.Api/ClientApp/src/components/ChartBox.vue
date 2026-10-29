<template>
  <div :style="{ position: 'relative', height: height + 'px' }">
    <canvas ref="canvas"></canvas>
  </div>
</template>

<script setup>
import { onMounted, onBeforeUnmount, ref, watch, markRaw } from 'vue'
import {
  Chart, LineController, BarController, LineElement, BarElement, PointElement,
  CategoryScale, LinearScale, Legend, Tooltip, Filler
} from 'chart.js'

Chart.register(LineController, BarController, LineElement, BarElement, PointElement, CategoryScale, LinearScale, Legend, Tooltip, Filler)

const props = defineProps({
  type: { type: String, default: 'line' },
  labels: { type: Array, default: () => [] },
  datasets: { type: Array, default: () => [] },
  options: { type: Object, default: () => ({}) },
  height: { type: Number, default: 260 }
})

const canvas = ref(null)
let chart = null

function render() {
  if (chart) {
    chart.data.labels = props.labels
    chart.data.datasets = props.datasets
    chart.update()
    return
  }
  chart = markRaw(new Chart(canvas.value, {
    type: props.type,
    data: { labels: props.labels, datasets: props.datasets },
    options: Object.assign({ responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false } }, props.options)
  }))
}

onMounted(render)
watch(() => [props.labels, props.datasets], render, { deep: true })
onBeforeUnmount(() => chart && chart.destroy())
</script>
