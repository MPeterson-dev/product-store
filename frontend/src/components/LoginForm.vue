<script setup>
import { ref } from 'vue'
import { apiFetch } from '../services/api.js'

const emit = defineEmits(['signed-in'])

const email = ref('')
const password = ref('')
const submitting = ref(false)
const error = ref('')

async function login() {
  if (submitting.value) return

  submitting.value = true
  error.value = ''

  try {
    const response = await apiFetch('/api/auth/login?useCookies=true', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: email.value.trim(),
        password: password.value,
      }),
    })

    if (!response.ok) {
      error.value = 'Unable to sign in. Check your email and password.'
      return
    }

    emit('signed-in')
  } catch {
    error.value = 'Could not reach the server. Please try again.'
  } finally {
    password.value = ''
    submitting.value = false
  }
}
</script>

<template>
  <section class="card shadow-sm mx-auto login-form">
    <div class="card-body p-4">
      <h1 class="h3 mb-4">Sign in</h1>

      <form @submit.prevent="login">
        <fieldset :disabled="submitting">
          <div class="mb-3">
            <label for="login-email" class="form-label">Email</label>
            <input
              id="login-email"
              v-model="email"
              type="email"
              autocomplete="username"
              class="form-control"
              required
            />
          </div>

          <div class="mb-4">
            <label for="login-password" class="form-label">Password</label>
            <input
              id="login-password"
              v-model="password"
              type="password"
              autocomplete="current-password"
              class="form-control"
              required
            />
          </div>

          <button type="submit" class="btn btn-dark w-100">
            {{ submitting ? 'Signing in…' : 'Sign in' }}
          </button>
        </fieldset>

        <p v-if="error" class="alert alert-danger mt-3 mb-0" role="alert">
          {{ error }}
        </p>
      </form>
    </div>
  </section>
</template>

<style scoped>
.login-form {
  max-width: 440px;
}
</style>
