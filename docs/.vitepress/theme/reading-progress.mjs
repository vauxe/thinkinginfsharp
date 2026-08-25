export function readingProgress(scrollTop, scrollHeight, viewportHeight) {
  const readableDistance = Math.max(0, scrollHeight - viewportHeight)
  if (readableDistance === 0) return 0
  return Math.min(1, Math.max(0, scrollTop / readableDistance))
}
