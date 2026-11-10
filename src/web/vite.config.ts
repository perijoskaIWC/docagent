import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Aspire injects the API address for WithReference(api). The name depends on the Aspire version.
const apiTarget =
  process.env.API_HTTPS ?? process.env.API_HTTP ??
  process.env.services__api__https__0 ?? process.env.services__api__http__0 ??
  'http://localhost:5080'

export default defineConfig(({ command }) => {
  // Shown in the web resource's console log in the Aspire dashboard.
  if (command === 'serve') console.log(`[docagent] proxying /api to ${apiTarget}`)

  return {
    plugins: [react()],
    server: {
      port: process.env.PORT ? Number(process.env.PORT) : 5173,
      // The browser only talks to this server; /api calls are forwarded to the API, so there is no CORS to set up.
      // secure: false accepts the API's local development certificate.
      proxy: { '/api': { target: apiTarget, changeOrigin: true, secure: false } },
    },
  }
})
