# Standalone Chat Application

A simple React-based chat interface that connects to your custom AI service running on `localhost:5991`.

## Features

- Clean, modern chat interface
- Real-time messaging with your AI agent
- Error handling for connection issues
- Typing indicators
- Message timestamps
- Responsive design

## Setup

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Configure your service endpoint:**
   The chat is configured to connect to `http://localhost:5991` by default. You can modify this in `src/components/Chat.tsx`:
   ```typescript
   const API_ENDPOINT = 'http://localhost:5991';
   ```

3. **Start the development server:**
   ```bash
   npm run dev
   ```

4. **Open your browser:**
   Navigate to `http://localhost:3000`

## API Requirements

Your service at `localhost:5991` should expose an endpoint that accepts POST requests. The default configuration expects:

**Endpoint:** `POST /api/chat`

**Request format:**
```json
{
  "message": "User's message text"
}
```

**Response format:**
```json
{
  "message": "AI agent's response"
}
```

## Customization

### Change the API endpoint
Edit the `API_ENDPOINT` constant in `src/components/Chat.tsx`.

### Modify request/response format
Update the `sendMessage` function in `src/components/Chat.tsx` to match your service's API specification.

### Add authentication
If your service requires authentication, add the necessary headers in the axios request:
```typescript
headers: {
  'Content-Type': 'application/json',
  'Authorization': 'Bearer your-token',
}
```

### Styling
Modify `src/components/Chat.css` to customize the appearance of the chat interface.

## Building for Production

```bash
npm run build
```

This will create a `dist` folder with the built application ready for deployment.

## Troubleshooting

- **Connection errors:** Make sure your service is running on the configured port (5991 by default)
- **CORS issues:** Your service may need to include CORS headers to allow requests from the frontend
- **API format:** Ensure your service accepts and returns data in the expected format

## Example Service Response

Your service should return a JSON response with at least a `message` field:

```json
{
  "message": "Hello! How can I help you today?",
  "timestamp": "2025-07-04T12:00:00Z",
  "conversation_id": "optional-conversation-id"
}
```
