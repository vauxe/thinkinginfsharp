<script setup>
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vitepress'
import { readingProgress } from './reading-progress.mjs'

const route = useRoute()
const progress = ref(0)
let animationFrame
let mounted = false

function updateProgress() {
  animationFrame = undefined
  const root = document.documentElement
  progress.value = readingProgress(root.scrollTop, root.scrollHeight, root.clientHeight)
}

function scheduleUpdate() {
  if (mounted && animationFrame === undefined) {
    animationFrame = requestAnimationFrame(updateProgress)
  }
}

onMounted(() => {
  mounted = true
  window.addEventListener('scroll', scheduleUpdate, { passive: true })
  window.addEventListener('resize', scheduleUpdate)
  document.fonts?.ready.then(scheduleUpdate)
  scheduleUpdate()
})

watch(() => route.path, () => nextTick(scheduleUpdate))

onBeforeUnmount(() => {
  mounted = false
  window.removeEventListener('scroll', scheduleUpdate)
  window.removeEventListener('resize', scheduleUpdate)
  if (animationFrame !== undefined) cancelAnimationFrame(animationFrame)
})
</script>

<template>
  <div class="reading-progress" aria-hidden="true">
    <span
      class="reading-progress__value"
      :style="{ transform: `scaleX(${progress})` }"
    />
  </div>
</template>
