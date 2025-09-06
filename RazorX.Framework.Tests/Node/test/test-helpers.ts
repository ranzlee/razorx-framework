import { vi } from 'vitest'

/**
 * Mock storage that can be configured to throw errors
 */
export class MockStorage implements Storage {
  private data: Record<string, string> = {}
  private shouldThrow = false
  private throwOnKeys: Set<string> = new Set()

  get length(): number {
    return Object.keys(this.data).length
  }

  clear(): void {
    if (this.shouldThrow) throw new Error('Storage access denied')
    this.data = {}
  }

  getItem(key: string): string | null {
    if (this.shouldThrow || this.throwOnKeys.has(key)) {
      throw new Error(`Failed to read storage key '${key}'`)
    }
    return this.data[key] ?? null
  }

  setItem(key: string, value: string): void {
    if (this.shouldThrow || this.throwOnKeys.has(key)) {
      throw new Error(`Failed to write storage key '${key}'`)
    }
    this.data[key] = value
  }

  removeItem(key: string): void {
    if (this.shouldThrow || this.throwOnKeys.has(key)) {
      throw new Error(`Failed to remove storage key '${key}'`)
    }
    delete this.data[key]
  }

  key(index: number): string | null {
    const keys = Object.keys(this.data)
    return keys[index] ?? null
  }

  // Test helpers
  setShouldThrow(value: boolean): void {
    this.shouldThrow = value
  }

  setThrowOnKey(key: string): void {
    this.throwOnKeys.add(key)
  }

  clearThrowOnKeys(): void {
    this.throwOnKeys.clear()
  }

  reset(): void {
    this.data = {}
    this.shouldThrow = false
    this.throwOnKeys.clear()
  }
}

/**
 * Mock fetch that can simulate various error conditions
 */
export function createMockFetch(options: {
  shouldFail?: boolean
  status?: number
  statusText?: string
  headers?: Record<string, string>
  body?: string
  delay?: number
  onRequest?: (url: string, init?: RequestInit) => void
}) {
  return vi.fn(async (url: string, init?: RequestInit) => {
    options.onRequest?.(url, init)
    
    if (options.delay) {
      await new Promise(resolve => setTimeout(resolve, options.delay))
    }

    if (options.shouldFail) {
      throw new Error('Network request failed')
    }

    return new Response(options.body ?? '', {
      status: options.status ?? 200,
      statusText: options.statusText ?? 'OK',
      headers: new Headers(options.headers ?? {})
    })
  })
}

/**
 * Helper to simulate browser detection
 */
export function mockBrowserDetection(browser: 'firefox' | 'chrome' | 'safari' | 'edge') {
  const userAgents = {
    firefox: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/109.0',
    chrome: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36',
    safari: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 13_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.1 Safari/605.1.15',
    edge: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36 Edg/109.0.1518.55'
  }

  Object.defineProperty(navigator, 'userAgent', {
    value: userAgents[browser],
    writable: true,
    configurable: true
  })
}

/**
 * Helper to create response with specific headers
 */
export function createResponseWithHeaders(headers: Record<string, string>, body = '', status = 200) {
  return new Response(body, {
    status,
    headers: new Headers(headers)
  })
}

/**
 * Helper to test memory cleanup
 */
export function trackWeakMapSize(weakMap: WeakMap<object, unknown>): () => number {
  let count = 0
  const originalSet = weakMap.set
  const originalDelete = weakMap.delete

  weakMap.set = function(key: object, value: unknown) {
    count++
    return originalSet.call(this, key, value)
  }

  weakMap.delete = function(key: object) {
    const result = originalDelete.call(this, key)
    if (result) count--
    return result
  }

  return () => count
}

/**
 * Helper to create elements with specific attributes and trigger mutation observer
 */
export function createAndObserveElement(
  tagName: string,
  attributes: Record<string, string>,
  triggerObserver: (elements: Element[]) => void
): HTMLElement {
  const element = document.createElement(tagName)
  Object.entries(attributes).forEach(([key, value]) => {
    element.setAttribute(key, value)
  })
  document.body.appendChild(element)
  triggerObserver([element])
  return element
}

/**
 * Helper to simulate JSON parse errors
 */
export function createMalformedJSON(): string {
  return '{"invalid": json"}'
}

/**
 * Helper to wait for async operations with timeout
 */
export async function waitForCondition(
  condition: () => boolean,
  timeout = 1000,
  checkInterval = 10
): Promise<void> {
  const startTime = Date.now()
  
  while (!condition()) {
    if (Date.now() - startTime > timeout) {
      throw new Error('Condition not met within timeout')
    }
    await new Promise(resolve => setTimeout(resolve, checkInterval))
  }
}

/**
 * Mock IntersectionObserver with controllable behavior
 */
export class MockIntersectionObserver {
  private static instances: MockIntersectionObserver[] = []
  private callback: IntersectionObserverCallback
  private elements = new Set<Element>()
  
  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback
    MockIntersectionObserver.instances.push(this)
  }

  observe(element: Element): void {
    this.elements.add(element)
  }

  unobserve(element: Element): void {
    this.elements.delete(element)
  }

  disconnect(): void {
    this.elements.clear()
  }

  // Test helper to trigger intersection
  static triggerIntersection(element: Element, isIntersecting: boolean): void {
    MockIntersectionObserver.instances.forEach(observer => {
      if (observer.elements.has(element)) {
        const entry = {
          target: element,
          isIntersecting,
          intersectionRatio: isIntersecting ? 1 : 0,
          boundingClientRect: element.getBoundingClientRect(),
          intersectionRect: element.getBoundingClientRect(),
          rootBounds: null,
          time: performance.now()
        } as IntersectionObserverEntry

        observer.callback([entry], observer as unknown as IntersectionObserver)
      }
    })
  }

  static reset(): void {
    MockIntersectionObserver.instances = []
  }
}