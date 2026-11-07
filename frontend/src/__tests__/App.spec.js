import { afterEach, describe, it, expect, vi } from 'vitest'
import { createPinia } from 'pinia'
import { mount, flushPromises } from '@vue/test-utils'
import App from '../App.vue'
import Builder from '../components/OwnerProductBuilder.vue'

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

describe('owner dashboard', () => {
  it('hides the store header when opening the builder', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [] }))
    const wrapper = mount(App, { global: { plugins: [createPinia()] } })
    await flushPromises()
    await wrapper.get('[aria-label="Account options"]').trigger('click')
    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Owner Dashboard')
      .trigger('click')
    expect(wrapper.find('header').exists()).toBe(false)
    expect(wrapper.findComponent(Builder).exists()).toBe(true)
    wrapper.unmount()
  })

  it('previews a detail and submits it without clothing options', async () => {
    const product = { id: 4, name: 'Cup', sizes: [], colors: [] }
    const mock = vi.fn().mockResolvedValue({ ok: true, json: async () => product })
    vi.stubGlobal('fetch', mock)
    const wrapper = mount(Builder)
    await wrapper.get('#product-title').setValue('Cup')
    await wrapper.get('#product-description').setValue('Ceramic cup')
    await wrapper.get('#product-category').setValue('Drinkware')
    await wrapper.get('#product-price').setValue('18')
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Add descriptive'))
      .trigger('click')
    await wrapper.get('#detail-name-0').setValue('Capacity')
    await wrapper.get('#detail-value-0').setValue('15 oz')
    expect(wrapper.get('aside').text()).toContain('15 oz')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(JSON.parse(mock.mock.calls[0][1].body).details).toEqual([
      { name: 'Capacity', value: '15 oz' },
    ])
    expect(wrapper.emitted('created')[0][0]).toEqual(product)
    wrapper.unmount()
  })
})

describe('product photo', () => {
  it('previews a selected photo and includes its uploaded URL and alt text when creating', async () => {
    const createUrl = vi.fn().mockReturnValue('blob:preview')
    const revokeUrl = vi.fn()
    vi.stubGlobal('URL', { createObjectURL: createUrl, revokeObjectURL: revokeUrl })
    const imageUrl = '/api/product-images/11111111111111111111111111111111.png'
    const mock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, json: async () => ({ imageUrl }) })
      .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 4, name: 'Cup', imageUrl }) })
    vi.stubGlobal('fetch', mock)
    const wrapper = mount(Builder)
    await wrapper.get('#product-title').setValue('Cup')
    await wrapper.get('#product-description').setValue('Ceramic cup')
    await wrapper.get('#product-category').setValue('Drinkware')
    await wrapper.get('#product-price').setValue('18')
    const input = wrapper.get('#product-photo')
    const photo = new File(['image'], 'cup.png', { type: 'image/png' })
    Object.defineProperty(input.element, 'files', { value: [photo], configurable: true })
    await input.trigger('change')
    await wrapper.get('#photo-alt').setValue('A blue cup')
    expect(wrapper.get('aside img').attributes('src')).toBe('blob:preview')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(mock.mock.calls[0][0]).toBe('/api/product-images')
    expect(mock.mock.calls[0][1].body).toBe(photo)
    const payload = JSON.parse(mock.mock.calls[1][1].body)
    expect(payload.imageUrl).toBe(imageUrl)
    expect(payload.imageAlt).toBe('A blue cup')
    expect(revokeUrl).toHaveBeenCalledWith('blob:preview')
    wrapper.unmount()
  })

  it('rejects an unsupported file before uploading', async () => {
    const mock = vi.fn()
    vi.stubGlobal('fetch', mock)
    const wrapper = mount(Builder)
    const input = wrapper.get('#product-photo')
    Object.defineProperty(input.element, 'files', {
      value: [new File(['svg'], 'image.svg', { type: 'image/svg+xml' })],
    })
    await input.trigger('change')
    expect(wrapper.get('[role="alert"]').text()).toContain('JPEG, PNG, or WebP')
    expect(mock).not.toHaveBeenCalled()
    wrapper.unmount()
  })
})
