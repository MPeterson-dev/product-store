<script setup>
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import ProductCard from './ProductCard.vue'
import { apiFetch } from '../services/api.js'

const props = defineProps({ product: { type: Object, default: null } })
const emit = defineEmits(['created', 'updated', 'cancel'])
const isEditing = computed(() => props.product !== null)
const draft = reactive({ name: '', description: '', category: '', price: '', details: [] })
const saving = ref(false)
const error = ref('')
const success = ref('')
let nextDetailId = 0
const photo = ref(null)
const photoPreview = ref('')
const photoAlt = ref('')
const photoError = ref('')
const photoInput = ref(null)
let uploadedImageUrl = ''

function removePhoto() {
  if (photoPreview.value.startsWith('blob:')) URL.revokeObjectURL(photoPreview.value)
  photo.value = null
  photoPreview.value = ''
  photoAlt.value = ''
  photoError.value = ''
  uploadedImageUrl = ''
  if (photoInput.value) photoInput.value.value = ''
}

function selectPhoto(event) {
  const file = event.target.files?.[0]
  if (!file) return
  if (
    !['image/jpeg', 'image/png', 'image/webp'].includes(file.type) ||
    file.size === 0 ||
    file.size > 5 * 1024 * 1024
  ) {
    if (photoInput.value) photoInput.value.value = ''
    photoError.value = 'Choose a JPEG, PNG, or WebP image up to 5 MB.'
    return
  }
  removePhoto()
  photo.value = file
  photoPreview.value = URL.createObjectURL(file)
}

onBeforeUnmount(() => {
  if (photoPreview.value.startsWith('blob:')) URL.revokeObjectURL(photoPreview.value)
})

const preview = computed(() => ({
  ...draft,
  imageUrl: photoPreview.value,
  imageAlt: photoAlt.value,
  details: draft.details.filter((detail) => detail.name || detail.value),
}))

function addDetail() {
  draft.details.push({ id: nextDetailId++, name: '', value: '' })
}

// Copy into a draft: typing and Cancel never modify the storefront object.
watch(
  () => props.product,
  (product) => {
    removePhoto()
    error.value = ''
    success.value = ''
    Object.assign(draft, {
      name: product?.name ?? '',
      description: product?.description ?? '',
      category: product?.category ?? '',
      price: product?.price ?? '',
      details: (product?.details ?? []).map(({ name, value }) => ({
        id: nextDetailId++,
        name,
        value,
      })),
    })
    photoPreview.value = product?.imageUrl ?? ''
    uploadedImageUrl = product?.imageUrl ?? ''
    photoAlt.value = product?.imageAlt ?? ''
  },
  { immediate: true },
)

async function saveProduct() {
  if (saving.value) return
  error.value = ''
  success.value = ''
  const details = draft.details
    .filter((detail) => detail.name.trim() || detail.value.trim())
    .map(({ name, value }) => ({ name: name.trim(), value: value.trim() }))

  if (details.some((detail) => !detail.name || !detail.value)) {
    error.value = 'Enter both a name and a value for each descriptive field, or remove the row.'
    return
  }
  if (
    !draft.name.trim() ||
    !draft.description.trim() ||
    !draft.category.trim() ||
    !Number.isFinite(Number(draft.price)) ||
    Number(draft.price) <= 0
  ) {
    error.value = 'Enter a title, description, category, and price greater than zero.'
    return
  }
  if (new Set(details.map((detail) => detail.name.toLowerCase())).size !== details.length) {
    error.value = 'Use a different name for each descriptive field.'
    return
  }

  photoError.value = ''
  if (photoPreview.value && !photoAlt.value.trim()) {
    photoError.value = 'Describe the photo for shoppers using screen readers.'
    return
  }
  if (photoError.value) return

  saving.value = true
  try {
    if (photo.value && !uploadedImageUrl) {
      const upload = await apiFetch('/api/product-images', {
        method: 'POST',
        headers: { 'Content-Type': photo.value.type },
        body: photo.value,
      })
      const image = await upload.json()
      if (!upload.ok) {
        photoError.value = image.message ?? 'Photo upload failed. Try another image.'
        return
      }
      uploadedImageUrl = image.imageUrl
    }
    const url = isEditing.value ? `/api/products/${props.product.id}` : '/api/products'
    const response = await apiFetch(url, {
      method: isEditing.value ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: draft.name.trim(),
        description: draft.description.trim(),
        category: draft.category.trim(),
        price: Number(draft.price),
        details,
        imageUrl: uploadedImageUrl || null,
        imageAlt: photoAlt.value.trim(),
      }),
    })
    if (response.status === 401 || response.status === 403) {
      error.value =
        'Owner access is required. Your draft is still here; sign in again before saving.'
      return
    }
    const result = await response.json()
    if (!response.ok) {
      error.value = result.message ?? 'Could not save the product.'
      return
    }
    if (isEditing.value) {
      emit('updated', result)
      return
    }
    removePhoto()
    emit('created', result)
    success.value = `“${result.name}” was created. Use Back to store to see it.`
    Object.assign(draft, { name: '', description: '', category: '', price: '', details: [] })
  } catch {
    error.value = 'Could not confirm the save. Check the API and storefront before retrying.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="row g-4 align-items-start">
    <section class="col-12 col-lg-7" aria-labelledby="builder-title">
      <form class="card border-0 shadow-sm p-4" @submit.prevent="saveProduct">
        <h2 id="builder-title" class="h4 mb-4">
          {{ isEditing ? 'Edit product' : 'Create a product' }}
        </h2>
        <fieldset :disabled="saving">
          <div class="mb-4">
            <label for="product-photo" class="form-label">Main product photo (optional)</label>
            <input
              id="product-photo"
              ref="photoInput"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              class="form-control"
              aria-describedby="photo-help"
              @change="selectPhoto"
            />
            <p id="photo-help" class="small text-secondary mt-2">
              JPEG, PNG, or WebP, up to 5 MB. Your photo appears in the preview before publishing.
            </p>
            <div v-if="photoPreview">
              <button type="button" class="btn btn-outline-danger btn-sm mb-3" @click="removePhoto">
                Remove photo
              </button>
              <label for="photo-alt" class="form-label d-block">Image description (alt text)</label>
              <input
                id="photo-alt"
                v-model="photoAlt"
                class="form-control"
                required
                maxlength="250"
                placeholder="Blue ceramic cup with a curved handle"
              />
            </div>
            <p v-if="photoError" class="text-danger mt-2" role="alert">{{ photoError }}</p>
          </div>
          <div class="mb-3">
            <label for="product-title" class="form-label">Title</label>
            <input
              id="product-title"
              v-model="draft.name"
              class="form-control"
              required
              maxlength="120"
              placeholder="Blue ceramic cup"
            />
          </div>
          <div class="mb-3">
            <label for="product-description" class="form-label">Description</label>
            <textarea
              id="product-description"
              v-model="draft.description"
              class="form-control"
              required
              maxlength="2000"
              rows="4"
              placeholder="Tell shoppers about this product."
            />
          </div>
          <div class="row g-3 mb-4">
            <div class="col-sm-6">
              <label for="product-category" class="form-label">Category</label>
              <input
                id="product-category"
                v-model="draft.category"
                class="form-control"
                required
                maxlength="80"
                placeholder="Drinkware"
              />
            </div>
            <div class="col-sm-6">
              <label for="product-price" class="form-label">Price (USD)</label>
              <input
                id="product-price"
                v-model="draft.price"
                type="number"
                class="form-control"
                required
                min="0.01"
                max="1000000"
                step="0.01"
                placeholder="18.00"
              />
            </div>
          </div>

          <h3 class="h6">
            Descriptive fields <span class="text-secondary fw-normal">(optional)</span>
          </h3>
          <p class="small text-secondary">
            Describe anything: material, capacity, dimensions, etc. Not choices for shoppers.
          </p>
          <div
            v-for="(detail, index) in draft.details"
            :key="detail.id"
            class="border rounded p-3 mb-3"
          >
            <div class="row g-2">
              <div class="col-sm-5">
                <label :for="`detail-name-${detail.id}`" class="form-label small">Field name</label>
                <input
                  :id="`detail-name-${detail.id}`"
                  v-model="detail.name"
                  class="form-control"
                  maxlength="60"
                  placeholder="Capacity"
                />
              </div>
              <div class="col-sm-7">
                <label :for="`detail-value-${detail.id}`" class="form-label small">Value</label>
                <input
                  :id="`detail-value-${detail.id}`"
                  v-model="detail.value"
                  class="form-control"
                  maxlength="200"
                  placeholder="15 oz"
                />
              </div>
            </div>
            <button
              type="button"
              class="btn btn-link text-danger btn-sm px-0 mt-2"
              :aria-label="`Remove descriptive field ${index + 1}`"
              @click="draft.details.splice(index, 1)"
            >
              Remove field
            </button>
          </div>
          <button
            type="button"
            class="btn btn-outline-secondary mb-4"
            :disabled="draft.details.length >= 20"
            @click="addDetail"
          >
            + Add descriptive field
          </button>
          <div>
            <button type="submit" class="btn btn-dark">
              {{ saving ? 'Saving…' : isEditing ? 'Save changes' : 'Create product' }}
            </button>
            <button
              v-if="isEditing"
              type="button"
              class="btn btn-outline-secondary ms-2"
              @click="emit('cancel')"
            >
              Cancel
            </button>
          </div>
        </fieldset>
        <p v-if="error" class="alert alert-danger mt-3 mb-0" role="alert">{{ error }}</p>
        <p v-if="success" class="alert alert-success mt-3 mb-0" role="status">{{ success }}</p>
      </form>
    </section>
    <aside class="col-12 col-lg-5 preview-column" aria-labelledby="preview-title">
      <h2 id="preview-title" class="h5">Live product preview</h2>
      <p class="small text-secondary">This is how your product card will appear in the store.</p>
      <ProductCard :product="preview">
        <button type="button" class="btn btn-dark" disabled>Add to Cart</button>
      </ProductCard>
    </aside>
  </div>
</template>

<style scoped>
@media (min-width: 992px) {
  .preview-column {
    position: sticky;
    top: 1.5rem;
  }
}
</style>
