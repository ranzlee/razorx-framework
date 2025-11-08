import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    isolate: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov', 'json-summary'],
      exclude: [
        'node_modules/**',
        'dist/**',
        '**/*.d.ts',
        'test/**',
        'vitest.config.ts',
        'esbuild.mjs',
        'eslint.config.js'
      ],
      include: ['src/**/*.ts'],
      thresholds: {
        lines: 69,
        functions: 80,
        branches: 70,
        statements: 69
      },
      watermarks: {
        statements: [69, 85],
        functions: [80, 95],
        branches: [70, 90],
        lines: [69, 85]
      }
    },
    setupFiles: ['./test/setup.ts']
  }
})