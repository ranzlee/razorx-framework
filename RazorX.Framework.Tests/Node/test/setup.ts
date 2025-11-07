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

// Ensure Blob exists (JSDOM provides it, but make sure it's available)
if (typeof Blob === 'undefined') {
  throw new Error('Blob is not available in test environment')
}

// Mock File constructor for JSDOM - must ensure instanceof Blob works
// The key issue: JSDOM on Ubuntu may have Blob in a different realm
// Solution: Explicitly set the prototype chain after class creation
const OriginalBlob = globalThis.Blob

class FileImpl {
  name: string
  lastModified: number
  type: string
  size: number
  private _bits: BlobPart[]

  constructor(bits: BlobPart[], name: string, options?: FilePropertyBag) {
    this._bits = bits
    this.name = name
    this.type = options?.type || ''
    this.lastModified = options?.lastModified ?? Date.now()
    // Calculate size from bits
    this.size = bits.reduce((acc, bit) => {
      if (typeof bit === 'string') return acc + bit.length
      if (bit instanceof ArrayBuffer) return acc + bit.byteLength
      if (ArrayBuffer.isView(bit)) return acc + bit.byteLength
      return acc
    }, 0)
  }

  // Blob methods
  slice(start?: number, end?: number, contentType?: string): Blob {
    return new OriginalBlob(this._bits, { type: contentType })
  }

  async text(): Promise<string> {
    return new OriginalBlob(this._bits).text()
  }

  async arrayBuffer(): Promise<ArrayBuffer> {
    return new OriginalBlob(this._bits).arrayBuffer()
  }

  stream(): ReadableStream {
    return new OriginalBlob(this._bits).stream()
  }

  get [Symbol.toStringTag]() {
    return 'File'
  }
}

// Crucial: Set prototype to Blob.prototype so instanceof Blob works
Object.setPrototypeOf(FileImpl.prototype, OriginalBlob.prototype)

// Override global File
;(globalThis as unknown as Record<string, unknown>).File = FileImpl

// Verify instanceof works
const testFile = new (globalThis.File as typeof File)(['test'], 'test.txt')
if (!(testFile instanceof Blob)) {
  console.error('CRITICAL: File mock does not pass instanceof Blob check!')
  console.error('testFile instanceof Blob:', testFile instanceof Blob)
  console.error('testFile.constructor:', testFile.constructor.name)
  console.error('Blob:', Blob.name)
  console.error('Object.getPrototypeOf(testFile):', Object.getPrototypeOf(testFile))
  console.error('Object.getPrototypeOf(testFile.constructor.prototype):', Object.getPrototypeOf(testFile.constructor.prototype))
  throw new Error('File mock failed instanceof Blob verification')
}

// Mock navigator
Object.defineProperty(navigator, 'userAgent', {
  value: 'Mozilla/5.0 (Test Browser)',
  configurable: true
})

// Mock MutationObserver with proper callback handling
globalThis.MutationObserver = vi.fn().mockImplementation(function(callback: MutationCallback) {
  return {
    observe: vi.fn(),
    disconnect: vi.fn(),
    takeRecords: vi.fn(() => []),
    callback
  }
})

// Mock IntersectionObserver
globalThis.IntersectionObserver = vi.fn().mockImplementation(function() {
  return {
    observe: vi.fn(),
    unobserve: vi.fn(),
    disconnect: vi.fn()
  }
})

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

// Mock Popover API since jsdom doesn't implement it
HTMLElement.prototype.showPopover = vi.fn(function(this: HTMLElement) {
  // Simulate popover behavior
  this.setAttribute('popover-open', 'true')
})

HTMLElement.prototype.hidePopover = vi.fn(function(this: HTMLElement) {
  // Simulate popover behavior
  this.removeAttribute('popover-open')
})

// Add :popover-open pseudo-class support for testing
Object.defineProperty(HTMLElement.prototype, 'matches', {
  value: function(this: HTMLElement, selector: string) {
    if (selector === ':popover-open') {
      return this.hasAttribute('popover-open')
    }
    // Call the original matches for other selectors
    return Element.prototype.matches.call(this, selector)
  },
  writable: true,
  configurable: true
})

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