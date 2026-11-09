export async function apiFetch(url, options = {}) {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)

  const changesData = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)

  if (changesData) {
    const tokenResponse = await fetch('/api/auth/csrf', {
      credentials: 'same-origin',
      cache: 'no-store',
    })

    if (!tokenResponse.ok) {
      throw new Error('Unable to prepare a secure request.')
    }

    const { token } = await tokenResponse.json()
    headers.set('X-CSRF-TOKEN', token)
  }

  return fetch(url, {
    ...options,
    method,
    headers,
    credentials: 'same-origin',
  })
}
