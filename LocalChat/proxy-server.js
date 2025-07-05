const express = require('express');
const cors = require('cors');
const { createProxyMiddleware } = require('http-proxy-middleware');

const app = express();
const PORT = 5992;

// Enable CORS for all routes
app.use(cors({
  origin: ['http://localhost:3001', 'http://localhost:3000'],
  methods: ['GET', 'POST', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization', 'X-App-Name']
}));

// Proxy middleware to forward requests to your HTTPS agent
const proxyOptions = {
  target: 'https://localhost:5991',
  changeOrigin: true,
  secure: false, // Allow self-signed certificates
  logLevel: 'debug'
};

app.use('/api', createProxyMiddleware(proxyOptions));

app.listen(PORT, () => {
  console.log(`Proxy server running on http://localhost:${PORT}`);
  console.log(`Forwarding requests to https://localhost:5991`);
});
