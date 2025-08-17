import { beforeEach, vi } from 'vitest'

// Import types for proper typing
import type { ElementCallbacks } from '../src/razorx'

// Extended Document interface for custom properties
interface ExtendedDocument extends Document {
  rxMutationObserver: MutationObserver
}

// Extended HTMLElement interface for custom properties
interface ExtendedHTMLElement extends HTMLElement {
  addRxCallbacks?: (callbacks: ElementCallbacks) => void
  _rxCallbacks?: ElementCallbacks
}

// Mock global objects that might be used by razorx.ts
Object.defineProperty(window, 'location', {
  value: {
    href: 'http://localhost:3000',
    pathname: '/',
    search: '',
    assign: vi.fn(),
    reload: vi.fn()
  },
  writable: true
})

// Mock fetch
globalThis.fetch = vi.fn()

// Mock navigator
Object.defineProperty(navigator, 'userAgent', {
  value: 'Mozilla/5.0 (Test Browser)',
  configurable: true
})

// Mock MutationObserver with proper callback handling
globalThis.MutationObserver = vi.fn().mockImplementation((callback: MutationCallback) => ({
  observe: vi.fn(),
  disconnect: vi.fn(),
  takeRecords: vi.fn(() => []),
  callback
}))

// Mock IntersectionObserver
globalThis.IntersectionObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn()
}))

// Mock HTMLFormElement.requestSubmit since jsdom doesn't implement it properly
if (typeof HTMLFormElement.prototype.requestSubmit === 'function') {
  // Override existing (broken) implementation
  HTMLFormElement.prototype.requestSubmit = function(this: HTMLFormElement, submitter?: HTMLElement) {
    const event = new Event('submit', { bubbles: true, cancelable: true })
    if (submitter) {
      Object.defineProperty(event, 'submitter', { value: submitter, writable: false })
    }
    this.dispatchEvent(event)
  }
} else {
  // Define if it doesn't exist
  Object.defineProperty(HTMLFormElement.prototype, 'requestSubmit', {
    value: function(this: HTMLFormElement, submitter?: HTMLElement) {
      const event = new Event('submit', { bubbles: true, cancelable: true })
      if (submitter) {
        Object.defineProperty(event, 'submitter', { value: submitter, writable: false })
      }
      this.dispatchEvent(event)
    },
    writable: true,
    configurable: true
  })
}

// Mock HTMLDialogElement.close since jsdom doesn't implement it properly
if (typeof HTMLDialogElement !== 'undefined') {
  HTMLDialogElement.prototype.close = vi.fn()
  HTMLDialogElement.prototype.show = vi.fn()
  HTMLDialogElement.prototype.showModal = vi.fn()
} else {
  // Create HTMLDialogElement if it doesn't exist
  ;(globalThis as unknown as Record<string, unknown>).HTMLDialogElement = class HTMLDialogElement extends HTMLElement {
    close = vi.fn()
    show = vi.fn()
    showModal = vi.fn()
    open = false
    returnValue = ''
    
    constructor() {
      super()
    }
  }
}

// Mock sessionStorage and localStorage
Object.defineProperty(window, 'sessionStorage', {
  value: {
    getItem: vi.fn(() => null),
    setItem: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn()
  },
  writable: true
})

Object.defineProperty(window, 'localStorage', {
  value: {
    getItem: vi.fn(() => null),
    setItem: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn()
  },
  writable: true
})

// Reset all mocks before each test
beforeEach(() => {
  vi.clearAllMocks()
  
  // Thoroughly reset document state
  document.head.innerHTML = ''
  document.body.innerHTML = ''
  document.title = ''
  
  // Reset document properties
  const extDocument = document as ExtendedDocument
  if ('rxMutationObserver' in extDocument && extDocument.rxMutationObserver) {
    extDocument.rxMutationObserver.disconnect()
    delete (extDocument as unknown as Record<string, unknown>).rxMutationObserver
  }
  
  // Clear any timers
  vi.clearAllTimers()
  
  // More aggressive cleanup to prevent property redefinition errors
  try {
    // Clear all existing DOM elements to avoid property conflicts
    const allElements = document.querySelectorAll('*')
    allElements.forEach(el => {
      const htmlEl = el as ExtendedHTMLElement
      if (htmlEl.addRxCallbacks) {
        try {
          delete htmlEl.addRxCallbacks
        } catch {
          // Ignore deletion errors
        }
      }
      if (htmlEl._rxCallbacks) {
        try {
          delete htmlEl._rxCallbacks
        } catch {
          // Ignore deletion errors
        }
      }
    })
    
    // Also clean prototype
    const elementProto = HTMLElement.prototype as ExtendedHTMLElement
    if (Object.hasOwn(elementProto, 'addRxCallbacks')) {
      delete elementProto.addRxCallbacks
    }
    if (Object.hasOwn(elementProto, '_rxCallbacks')) {
      delete elementProto._rxCallbacks
    }
  } catch {
    // Ignore cleanup errors
  }
  
  // Reset storage mocks
  vi.mocked(sessionStorage.getItem).mockReturnValue(null)
  vi.mocked(localStorage.getItem).mockReturnValue(null)
  vi.mocked(sessionStorage.setItem).mockClear()
  vi.mocked(sessionStorage.removeItem).mockClear()
  vi.mocked(localStorage.setItem).mockClear()
  vi.mocked(localStorage.removeItem).mockClear()
})