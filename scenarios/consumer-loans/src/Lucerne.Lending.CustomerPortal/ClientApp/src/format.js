const money = new Intl.NumberFormat('fi-FI', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function eur (value) {
  if (value === null || value === undefined || value === '') return ''
  return money.format(Number(value)) + ' €'
}

export function date (value) {
  if (!value) return ''
  const d = new Date(value)
  return `${d.getDate()}.${d.getMonth() + 1}.${d.getFullYear()}`
}

export function percent (value) {
  if (value === null || value === undefined) return ''
  return Number(value).toFixed(2).replace('.', ',') + ' %'
}

export function ibanFormat (iban) {
  return (iban || '').replace(/\s+/g, '').toUpperCase().replace(/(.{4})/g, '$1 ').trim()
}

// mod-97 check, same rule as the lending API
export function ibanValid (iban) {
  const s = (iban || '').replace(/\s+/g, '').toUpperCase()
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$/.test(s)) return false
  const moved = s.slice(4) + s.slice(0, 4)
  let rest = 0
  for (const ch of moved) {
    const v = /[A-Z]/.test(ch) ? (ch.charCodeAt(0) - 55).toString() : ch
    for (const digit of v) rest = (rest * 10 + Number(digit)) % 97
  }
  return rest === 1
}
