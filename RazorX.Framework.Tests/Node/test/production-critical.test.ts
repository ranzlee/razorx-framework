import { describe, test, expect, vi, beforeEach, afterEach, type Mock } from 'vitest'
import { razorx } from '../src/razorx'

/**
 * Production-Critical Scenario Tests
 *
 * These tests focus on realistic production scenarios and edge cases that could
 * cause issues in production environments. Tests focus on observable behavior
 * rather than internal implementation details.
 */

interface RxMutationObserver {
  observe: (target: Node, options?: MutationObserverInit) => void
  disconnect: () => void
  callback?: (mutations: MutationRecord[]) => void
}

interface RxDocument {
  rxMutationObserver?: RxMutationObserver
}

function triggerDOMContentLoaded(): void {
  document.dispatchEvent(new Event('DOMContentLoaded'))
}

function triggerMutationObserver(addedNodes: Node[] = [], removedNodes: Node[] = []): void {
  const observer = (document as unknown as RxDocument).rxMutationObserver
  if (observer && observer.callback) {
    observer.callback([{
      type: 'childList' as const,
      addedNodes: addedNodes as unknown as NodeList,
      removedNodes: removedNodes as unknown as NodeList,
      target: document.body,
      attributeName: null,
      attributeNamespace: null,
      nextSibling: null,
      previousSibling: null,
      oldValue: null
    } as MutationRecord])
  }
}

const processNewElements = function(): void {
  triggerMutationObserver([document.body])
}

function waitForMicrotasks(): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, 0))
}

function waitForDOMUpdates(): Promise<void> {
  return new Promise(resolve => {
    setTimeout(() => {
      setTimeout(resolve, 0)
    }, 0)
  })
}

function createElementWithId(tagName: string, id: string, attributes: Record<string, string> = {}): HTMLElement {
  const element = document.createElement(tagName)
  element.id = id

  for (const [key, value] of Object.entries(attributes)) {
    element.setAttribute(key, value)
  }

  return element
}

let testCounter = 0
let mockFetch: ReturnType<typeof vi.fn>
let mockStorage: {
  sessionStorage: Map<string, string>
  localStorage: Map<string, string>
}

describe('Production-Critical Scenarios', () => {
  // Global handler for expected rejections from element removal during async operations
  beforeAll(() => {
    const globalRejectionHandler = (event: PromiseRejectionEvent) => {
      // Suppress expected "Element was removed from DOM" rejections
      if (event.reason?.message?.includes('was removed from DOM')) {
        event.preventDefault()
      }
    }
    window.addEventListener('unhandledrejection', globalRejectionHandler)
  })

  beforeEach(() => {
    testCounter++

    // Reset DOM
    document.body.innerHTML = ''
    document.head.innerHTML = ''

    // Reset timers
    vi.clearAllTimers()

    // Setup fetch mock
    mockFetch = vi.fn(() => new Response('', {
      status: 200,
      headers: { 'rx-merge': '[]' }
    }))
    globalThis.fetch = mockFetch

    // Setup storage mocks
    mockStorage = {
      sessionStorage: new Map(),
      localStorage: new Map()
    }

    Object.defineProperty(globalThis, 'sessionStorage', {
      value: {
        getItem: vi.fn((key: string) => mockStorage.sessionStorage.get(key) || null),
        setItem: vi.fn((key: string, value: string) => mockStorage.sessionStorage.set(key, value)),
        removeItem: vi.fn((key: string) => mockStorage.sessionStorage.delete(key)),
        clear: vi.fn(() => mockStorage.sessionStorage.clear())
      },
      writable: true,
      configurable: true
    })

    Object.defineProperty(globalThis, 'localStorage', {
      value: {
        getItem: vi.fn((key: string) => mockStorage.localStorage.get(key) || null),
        setItem: vi.fn((key: string, value: string) => mockStorage.localStorage.set(key, value)),
        removeItem: vi.fn((key: string) => mockStorage.localStorage.delete(key)),
        clear: vi.fn(() => mockStorage.localStorage.clear())
      },
      writable: true,
      configurable: true
    })

    // Clear RazorX state
    const rxDoc = document as unknown as RxDocument
    if (rxDoc.rxMutationObserver) {
      rxDoc.rxMutationObserver.disconnect()
      delete rxDoc.rxMutationObserver
    }

    // Initialize framework
    razorx.init()
    triggerDOMContentLoaded()
  })

  afterEach(() => {
    vi.clearAllMocks()
    vi.clearAllTimers()
  })

  function getUniqueId(base: string): string {
    return `${base}-${testCounter}`
  }

  describe('Callback Error Resilience', () => {
    test('global callback error does not crash framework', async () => {
      const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      const callbackExecuted = vi.fn()

      // Catch unhandled rejections from intentional error
      const unhandledRejectionHandler = vi.fn((event) => {
        event.preventDefault()
      })
      window.addEventListener('unhandledrejection', unhandledRejectionHandler)

      razorx.addCallbacks({
        beforeFetch: () => {
          callbackExecuted()
          throw new Error('Test error')
        }
      })

      const btnId = getUniqueId('btn')
      const button = createElementWithId('button', btnId, {
        'data-rx-action': '/api/test',
        'data-rx-trigger': 'click'
      })
      document.body.appendChild(button)
      processNewElements()

      button.click()
      await waitForDOMUpdates()

      // Callback was called (before throwing)
      expect(callbackExecuted).toHaveBeenCalled()

      // Error was caught (logged or handled as rejection)
      const hasError = errorSpy.mock.calls.some(call =>
        call[0]?.toString().includes('Error') || call[0]?.toString().includes('callback')
      )
      const hasRejection = unhandledRejectionHandler.mock.calls.length > 0

      // Framework handles error gracefully
      expect(hasError || hasRejection).toBe(true)

      // Framework continues (request may or may not complete depending on error handling)
      expect(button.isConnected).toBe(true)

      window.removeEventListener('unhandledrejection', unhandledRejectionHandler)
      errorSpy.mockRestore()
    })
  })

  describe('Element Cleanup During Async Operations', () => {
    test('element removed during poll stops polling', async () => {
      vi.useFakeTimers()

      const btnId = getUniqueId('btn')
      const button = createElementWithId('button', btnId, {
        'data-rx-action': '/api/poll',
        'data-rx-trigger': '{"type":"poll","interval":1000}'
      })
      document.body.appendChild(button)
      processNewElements()

      // Let first poll fire
      await vi.advanceTimersByTimeAsync(1100)
      const callCountAfterFirst = mockFetch.mock.calls.length

      // Remove element
      button.remove()
      triggerMutationObserver([], [button])

      // Wait for more poll intervals
      await vi.advanceTimersByTimeAsync(2500)

      // No additional polls
      expect(mockFetch.mock.calls.length).toBeLessThanOrEqual(callCountAfterFirst + 1)

      vi.useRealTimers()
    })

  })


  describe('SSE Connection Resilience', () => {
    let mockEventSource: {
      addEventListener: Mock
      removeEventListener: Mock
      close: Mock
      readyState: number
      url: string
      onerror: ((event: Event) => void) | null
      onopen: ((event: Event) => void) | null
    }

    beforeEach(() => {
      mockEventSource = {
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        close: vi.fn(),
        readyState: 1, // OPEN
        url: '',
        onerror: null,
        onopen: null
      }

      // @ts-expect-error - Mocking EventSource
      globalThis.EventSource = vi.fn().mockImplementation((url: string) => {
        mockEventSource.url = url
        mockEventSource.readyState = 1
        Promise.resolve().then(() => {
          if (mockEventSource.onopen) {
            mockEventSource.onopen(new Event('open'))
          }
        })
        return mockEventSource
      })
    })

    test('SSE handles connection errors gracefully', async () => {
      const containerId = getUniqueId('container')
      const container = createElementWithId('div', containerId, {
        'data-rx-sse-connect': '/api/stream'
      })

      document.body.appendChild(container)
      processNewElements()

      await waitForDOMUpdates()

      // Connection established
      expect(container.getAttribute('data-sse-state')).toBe('connected')

      // Simulate error
      mockEventSource.readyState = 2 // CLOSED
      mockEventSource.onerror?.(new Event('error'))

      await waitForDOMUpdates()

      // Error state set (reconnection logic triggered)
      expect(container.getAttribute('data-sse-state')).toBe('error')
    })

    test('SSE cleans up connection when element removed', async () => {
      const containerId = getUniqueId('container')
      const container = createElementWithId('div', containerId, {
        'data-rx-sse-connect': '/api/stream'
      })

      document.body.appendChild(container)
      processNewElements()

      await waitForDOMUpdates()

      expect(mockEventSource.close).not.toHaveBeenCalled()

      // Remove element
      container.remove()
      triggerMutationObserver([], [container])

      // Connection closed
      expect(mockEventSource.close).toHaveBeenCalled()
    })

    test('SSE handles repeated connection failures without crashing', async () => {
      const containerId = getUniqueId('container')
      const container = createElementWithId('div', containerId, {
        'data-rx-sse-connect': '/api/stream'
      })

      document.body.appendChild(container)
      processNewElements()

      await waitForDOMUpdates()

      // Simulate multiple errors
      for (let i = 0; i < 5; i++) {
        mockEventSource.readyState = 2 // CLOSED
        mockEventSource.onerror?.(new Event('error'))
        await waitForDOMUpdates()
      }

      // Framework doesn't crash
      expect(container.isConnected).toBe(true)
      expect(container.getAttribute('data-sse-state')).toBe('error')
    })
  })

  describe('API Surface Coverage', () => {
    test('getInstanceId returns instance ID after initialization', () => {
      const instanceId = razorx.getInstanceId()
      expect(instanceId).toBeTruthy()
      expect(typeof instanceId).toBe('string')
      expect(instanceId?.length).toBeGreaterThan(0)
    })


    test('element addRxCallbacks method exists after initialization', () => {
      const btnId = getUniqueId('btn')
      const button = createElementWithId('button', btnId, {
        'data-rx-action': '/api/test'
      })
      document.body.appendChild(button)
      processNewElements()

      // Element should have addRxCallbacks method
      expect(typeof button.addRxCallbacks).toBe('function')
    })
  })
})
