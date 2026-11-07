<script setup>
import { computed, onMounted, ref } from 'vue'
import { useCartStore } from './stores/cart.js'
import OwnerProductBuilder from './components/OwnerProductBuilder.vue'
import ProductCard from './components/ProductCard.vue'

// ref = Reactive values: Vue updates the page when these change.
const products = ref([])
const loading = ref(true)
const error = ref('')
const selectedCategory = ref('All')
const searchQuery = ref('')
const cart = useCartStore()
const selections = ref({})
const accountMenuOpen = ref(false)
const currentView = ref('store')
const checkingOut = ref(false)
const checkoutError = ref('')
const confirmation = ref(null)

async function checkout() {
  if (checkingOut.value || cart.items.length === 0) return

  checkingOut.value = true
  checkoutError.value = ''
  confirmation.value = null

  try {
    const response = await fetch('/api/checkout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        items: cart.items.map(({ id, size, color, quantity }) => ({
          productId: id,
          size,
          color,
          quantity,
        })),
      }),
    })

    const result = await response.json()
    if (!response.ok) {
      checkoutError.value = result.message ?? 'Checkout failed. Please try again.'
      return
    }

    confirmation.value = result
  } catch {
    checkoutError.value = 'Could not complete checkout. Make sure the API is running.'
  } finally {
    checkingOut.value = false
  }
}

const categories = computed(() => [
  'All',
  ...new Set(products.value.map((product) => product.category)),
])

const filteredProducts = computed(() =>
  products.value.filter((product) => {
    const matchesCategory =
      selectedCategory.value === 'All' || product.category === selectedCategory.value

    const matchesSearch = product.name
      .toLowerCase()
      .includes(searchQuery.value.trim().toLowerCase())

    return matchesCategory && matchesSearch
  }),
)

// Load products after the component appears on the page.
onMounted(async () => {
  try {
    const response = await fetch('/api/products')

    // Treat unsuccessful HTTP responses as errors.
    if (!response.ok) {
      throw new Error('Unable to load products.')
    }

    products.value = await response.json()
  } catch {
    error.value = 'Could not load products. Please try again later.'
  } finally {
    loading.value = false
  }

  selections.value = Object.fromEntries(
    products.value.map((product) => [
      product.id,
      { size: product.sizes[0] ?? '', color: product.colors[0] ?? '' },
    ]),
  )
})

function productCreated(product) {
  selections.value[product.id] = { size: product.sizes[0] ?? '', color: product.colors[0] ?? '' }
  products.value.push(product)
  searchQuery.value = ''
  selectedCategory.value = 'All'
}

function openOwnerDashboard() {
  currentView.value = 'owner'
  accountMenuOpen.value = false
}
</script>

<template>
  <header v-if="currentView === 'store'" class="border-bottom bg-white">
    <nav class="container py-3" aria-label="Store navigation">
      <div class="d-flex align-items-center gap-3 flex-wrap">
        <button
          type="button"
          class="btn btn-light fs-4"
          aria-label="Store home"
          @click="currentView = 'store'"
        >
          🛍️
        </button>

        <span class="text-secondary small"> 📍 Location Placeholder </span>

        <form class="flex-grow-1" role="search" @submit.prevent="currentView = 'store'">
          <label for="header-search" class="visually-hidden"> Search products </label>
          <input
            id="header-search"
            v-model="searchQuery"
            type="search"
            class="form-control"
            placeholder="Search products..."
          />
        </form>

        <button
          type="button"
          class="btn btn-outline-dark"
          :aria-label="`Shopping cart, ${cart.itemCount} items`"
          @click="currentView = 'store'"
        >
          🛒 <span>{{ cart.itemCount }}</span>
        </button>

        <div class="position-relative">
          <button
            type="button"
            class="btn btn-outline-dark"
            aria-label="Account options"
            aria-controls="account-options"
            :aria-expanded="accountMenuOpen"
            @click="accountMenuOpen = !accountMenuOpen"
          >
            👤
          </button>

          <div
            v-if="accountMenuOpen"
            id="account-options"
            class="position-absolute end-0 mt-2 p-2 bg-white border rounded shadow z-3"
          >
            <button @click="openOwnerDashboard">Owner Dashboard</button>
          </div>
        </div>
      </div>
    </nav>
  </header>
  <main v-if="currentView === 'store'" class="container py-5">
    <h1>Product seller here</h1>
    <div class="row g-3 mb-4">
      <div class="col-12 col-md-8">
        <label for="search" class="form-label">Search products</label>
        <input
          id="search"
          v-model="searchQuery"
          type="search"
          class="form-control"
          placeholder="Search"
        />
      </div>

      <div class="col-12 col-md-4">
        <label for="category" class="form-label">Filter by category</label>
        <select id="category" v-model="selectedCategory" class="form-select">
          <option v-for="category in categories" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
      </div>
    </div>
    <p v-if="loading">Loading products...</p>
    <p v-else-if="error" role="alert">{{ error }}</p>

    <ul v-else class="row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-3 list-unstyled">
      <li v-if="filteredProducts.length === 0" class="col-12">No products match your search.</li>
      <li v-for="product in filteredProducts" :key="product.id" class="col">
        <ProductCard :product="product">
          <label v-if="product.sizes.length" :for="`size-${product.id}`" class="form-label"
            >Size</label
          >
          <select
            v-if="product.sizes.length"
            :id="`size-${product.id}`"
            v-model="selections[product.id].size"
            class="form-select mb-2"
          >
            <option v-for="size in product.sizes" :key="size" :value="size">
              {{ size }}
            </option>
          </select>

          <label v-if="product.colors.length" :for="`color-${product.id}`" class="form-label"
            >Color</label
          >
          <select
            v-if="product.colors.length"
            :id="`color-${product.id}`"
            v-model="selections[product.id].color"
            class="form-select mb-3"
          >
            <option v-for="color in product.colors" :key="color" :value="color">
              {{ color }}
            </option>
          </select>

          <button
            type="button"
            class="btn btn-dark"
            @click="cart.addItem({ ...product, ...selections[product.id] })"
          >
            Add to Cart
          </button>
        </ProductCard>
      </li>
    </ul>
    <section class="mt-5">
      <h2 class="h4">Shopping cart ({{ cart.itemCount }})</h2>

      <p v-if="cart.items.length === 0">Your cart is empty.</p>

      <ul v-else class="list-group mb-3">
        <li v-for="item in cart.items" :key="item.variantKey" class="list-group-item">
          <h3 class="h6">{{ item.name }}</h3>
          <p v-if="item.size || item.color">
            {{ [item.size, item.color].filter(Boolean).join(' · ') }}
          </p>
          <p>${{ item.price.toFixed(2) }} each · Quantity: {{ item.quantity }}</p>

          <button
            type="button"
            class="btn btn-outline-secondary btn-sm me-2"
            :aria-label="`Decrease quantity of ${item.name}, size ${item.size}, ${item.color}`"
            @click="cart.decreaseQuantity(item.variantKey)"
          >
            −
          </button>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm me-2"
            :aria-label="`Increase quantity of ${item.name}, size ${item.size}, ${item.color}`"
            @click="cart.addItem(item)"
          >
            +
          </button>
          <button
            type="button"
            class="btn btn-outline-danger btn-sm"
            @click="cart.removeItem(item.variantKey)"
          >
            Remove
          </button>
        </li>
      </ul>

      <p>
        <strong>Total: ${{ cart.total.toFixed(2) }}</strong>
      </p>

      <button
        type="button"
        class="btn btn-dark"
        :disabled="checkingOut || cart.items.length === 0"
        @click="checkout"
      >
        {{ checkingOut ? 'Checking out…' : 'Demo checkout' }}
      </button>

      <p v-if="checkoutError" class="text-danger mt-3" role="alert">
        {{ checkoutError }}
      </p>

      <div v-if="confirmation" class="alert alert-success mt-3" role="status">
        <strong>Demo checkout complete: ${{ confirmation.total.toFixed(2) }}</strong>
        <div>Confirmation: {{ confirmation.confirmationId }}</div>
        <div>No payment was taken.</div>
      </div>
    </section>
  </main>
  <main v-else-if="currentView === 'owner'" class="container py-5">
    <button type="button" class="btn btn-outline-secondary mb-3" @click="currentView = 'store'">
      ← Back to store
    </button>
    <h1>Owner Dashboard</h1>
    <p class="text-secondary">Create and manage your products.</p>

    <p class="small text-secondary">Local demo: products reset when the API restarts.</p>
    <OwnerProductBuilder @created="productCreated" />
  </main>
</template>
