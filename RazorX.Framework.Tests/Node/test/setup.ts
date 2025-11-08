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

// Mock File constructor for JSDOM
class FileImpl extends Blob {
  name: string
  lastModified: number

  constructor(bits: BlobPart[], name: string, options?: FilePropertyBag) {
    super(bits, { type: options?.type || '' })
    this.name = name
    this.lastModified = options?.lastModified ?? Date.now()
  }

  get [Symbol.toStringTag](): string {
    return 'File'
  }
}

// Override global File
Object.defineProperty(globalThis, 'File', {
  value: FileImpl,
  writable: true,
  configurable: true,
  enumerable: false
})

// CRITICAL FIX: Wrap FormData constructor to handle File mocks
// JSDOM rejects our FileImpl in FormData.append() due to internal type checking
// Solution: Intercept FormData construction and manually add Files after
const OriginalFormData = globalThis.FormData
const fileStorage = new WeakMap<FormData, Map<string, FileImpl>>()

globalThis.FormData = class FormDataMock extends OriginalFormData {
  constructor(form?: HTMLFormElement, submitter?: HTMLElement | null) {
    fileStorage.set({} as any, new Map()) // Temporary placeholder

    if (form) {
      // Extract and temporarily remove file inputs to avoid JSDOM type checking
      const fileInputs: Array<{input: HTMLInputElement, files: FileList, parent: Node, nextSibling: Node | null}> = []
      const elements = Array.from(form.elements)

      for (const element of elements) {
        if ('type' in element && element.type === 'file') {
          const fileInput = element as HTMLInputElement
          if (fileInput.files && fileInput.files.length > 0) {
            // Save file input state
            fileInputs.push({
              input: fileInput,
              files: fileInput.files,
              parent: fileInput.parentNode!,
              nextSibling: fileInput.nextSibling
            })
            // Temporarily remove from DOM so native FormData doesn't see it
            fileInput.remove()
          }
        }
      }

      // Now call native FormData constructor - it will process all non-file inputs
      super(form, submitter)

      // Restore file inputs to DOM
      for (const {input, files, parent, nextSibling} of fileInputs) {
        if (nextSibling) {
          parent.insertBefore(input, nextSibling)
        } else {
          parent.appendChild(input)
        }
      }

      // Store files in WeakMap
      const fileMap = new Map<string, FileImpl>()
      for (const {input, files} of fileInputs) {
        if (input.name) {
          for (let j = 0; j < files.length; j++) {
            fileMap.set(input.name, files[j] as any)
          }
        }
      }
      fileStorage.set(this, fileMap)
    } else {
      // No form - just call super
      super(form, submitter)
      fileStorage.set(this, new Map())
    }
  }

  forEach(callback: (value: any, key: string, parent: FormData) => void, thisArg?: any): void {
    // Inject stored Files first
    const files = fileStorage.get(this)
    if (files) {
      files.forEach((file, key) => {
        callback.call(thisArg, file, key, this)
      })
    }
    // Then native FormData values
    super.forEach(callback, thisArg)
  }

  delete(name: string): void {
    fileStorage.get(this)?.delete(name)
    super.delete(name)
  }
} as any

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