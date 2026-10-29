const dateTime = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
const dateOnly = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
const hourOnly = new Intl.DateTimeFormat('en-GB', { weekday: 'short', hour: '2-digit', minute: '2-digit' })

export function fmtDateTime(v) {
  return v ? dateTime.format(new Date(v)) : '–'
}

export function fmtDate(v) {
  return v ? dateOnly.format(new Date(v)) : '–'
}

export function fmtHour(v) {
  return v ? hourOnly.format(new Date(v)) : '–'
}

export function fmtNumber(v, digits = 0) {
  if (v === null || v === undefined) return '–'
  return Number(v).toLocaleString('fi-FI', { minimumFractionDigits: digits, maximumFractionDigits: digits })
}

export function relative(v) {
  if (!v) return '–'
  const minutes = Math.round((Date.now() - new Date(v).getTime()) / 60000)
  if (Math.abs(minutes) < 1) return 'now'
  if (Math.abs(minutes) < 60) return minutes > 0 ? `${minutes} min ago` : `in ${-minutes} min`
  const hours = Math.round(minutes / 60)
  if (Math.abs(hours) < 48) return hours > 0 ? `${hours} h ago` : `in ${-hours} h`
  const days = Math.round(hours / 24)
  return days > 0 ? `${days} d ago` : `in ${-days} d`
}

export function delayHours(planned, actualOrEta) {
  if (!planned || !actualOrEta) return null
  return (new Date(actualOrEta) - new Date(planned)) / 3600000
}

export const statusText = {
  Requested: 'Requested',
  Booked: 'Booked',
  PickedUp: 'Picked up',
  InTransit: 'In transit',
  AtTerminal: 'At terminal',
  CustomsHold: 'Customs hold',
  OutForDelivery: 'Out for delivery',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled'
}
