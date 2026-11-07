<script setup>
defineProps({ product: { type: Object, required: true } })
</script>

<template>
  <article class="card h-100 shadow-sm">
    <img
      v-if="product.imageUrl"
      :src="product.imageUrl"
      :alt="product.imageAlt || product.name || 'Product photo'"
      class="card-img-top product-photo"
    />
    <div class="card-body">
      <p v-if="product.category" class="text-secondary small mb-2">{{ product.category }}</p>
      <h2 class="h5 card-title">{{ product.name || 'Product title' }}</h2>
      <p v-if="product.description" class="product-description">{{ product.description }}</p>
      <p class="fw-bold fs-5">
        {{
          new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(
            Number(product.price) || 0,
          )
        }}
      </p>
      <dl v-if="product.details?.length" class="mb-3">
        <div v-for="(detail, index) in product.details" :key="index" class="mb-2">
          <dt class="small text-secondary">{{ detail.name || 'Detail name' }}</dt>
          <dd class="mb-0">{{ detail.value || 'Detail value' }}</dd>
        </div>
      </dl>
      <slot />
    </div>
  </article>
</template>

<style scoped>
.product-photo {
  width: 100%;
  height: 260px;
  object-fit: contain;
  background: #f8f9fa;
}
.product-description {
  white-space: pre-line;
}
.card-body {
  overflow-wrap: anywhere;
}
</style>
