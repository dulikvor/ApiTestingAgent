const express = require('express');
const cors = require('cors');

const app = express();
const PORT = 3002;

// Store connected SSE clients for pushing agent messages
const agentMessageClients = new Set();

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.text({ type: 'text/event-stream' }));
app.use(express.text({ type: 'text/plain' }));

// SSE endpoint for UI to receive pushed agent messages
app.get('/api/agent-stream', (req, res) => {
    console.log('New agent-stream SSE client connected');
    
    res.setHeader('Content-Type', 'text/event-stream');
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('Connection', 'keep-alive');
    res.setHeader('X-Accel-Buffering', 'no');
    
    // Add client to set
    agentMessageClients.add(res);
    console.log(`Active agent-stream clients: ${agentMessageClients.size}`);
    
    // Send initial connection message
    res.write('event: connected\ndata: Agent stream connected\n\n');
    
    // Remove client on disconnect
    req.on('close', () => {
        agentMessageClients.delete(res);
        console.log(`Agent-stream client disconnected. Active clients: ${agentMessageClients.size}`);
    });
});

// Endpoint to receive messages from the agent (Dline)
// Accepts the same SSE format that LocalChatServerSentEventsStreamWriter produces
app.post('/api/agent-message', (req, res) => {
    try {
        const contentType = req.headers['content-type'] || '';
        
        console.log('Received agent message, content-type:', contentType);
        console.log('Raw body:', req.body);
        
        if (contentType.includes('text/event-stream') || contentType.includes('text/plain')) {
            // Parse SSE format from Dline - it sends raw text like:
            // {"message":"...","role":"..."}\n\n  (for chat messages)
            // event: function\ndata: [START] name\n\n  (for function events)
            const sseData = req.body;
            console.log('Parsing SSE data:', sseData);
            
            // Check if it already has SSE format (starts with "event:" or "data:")
            const trimmedData = sseData.trim();
            let formattedData;
            
            if (trimmedData.startsWith('event:') || trimmedData.startsWith('data:')) {
                // Already in SSE format, forward as-is
                formattedData = sseData;
            } else if (trimmedData.startsWith('{')) {
                // Raw JSON from Dline's chat message format - wrap in data:
                formattedData = `data: ${trimmedData}\n\n`;
            } else {
                // Unknown format, wrap in data:
                formattedData = `data: ${trimmedData}\n\n`;
            }
            
            console.log('Forwarding SSE data:', formattedData);
            
            // Forward to all connected clients
            agentMessageClients.forEach(client => {
                client.write(formattedData);
            });
            
            res.json({ status: 'success', clients: agentMessageClients.size });
        } else {
            // JSON format
            const messageData = req.body;
            console.log('Received JSON agent message:', messageData);
            
            // Convert to SSE format and broadcast
            const sseMessage = `data: ${JSON.stringify(messageData)}\n\n`;
            agentMessageClients.forEach(client => {
                client.write(sseMessage);
            });
            
            res.json({ status: 'success', clients: agentMessageClients.size });
        }
    } catch (error) {
        console.error('Error processing agent message:', error);
        res.status(500).json({ status: 'error', message: error.message });
    }
});

// Chat endpoint (original)
app.post('/api/chat', async (req, res) => {
    try {
        const { message } = req.body;
        
        console.log('Received message:', message);
        
        // Simple echo response - you can replace this with your AI logic
        const responseMessage = `Echo: ${message}`;
        
        // Add a small delay to simulate processing
        await new Promise(resolve => setTimeout(resolve, 500));
        
        res.json({
            message: responseMessage,
            timestamp: new Date().toISOString(),
            status: 'success'
        });
    } catch (error) {
        console.error('Server error:', error);
        res.status(500).json({
            message: `Error: ${error.message}`,
            status: 'error'
        });
    }
});

// Health check endpoint
app.get('/health', (req, res) => {
    res.json({ status: 'healthy', agentStreamClients: agentMessageClients.size });
});

app.listen(PORT, '0.0.0.0', () => {
    console.log(`Chat server running on http://0.0.0.0:${PORT}`);
    console.log('API endpoints:');
    console.log(`  - POST http://0.0.0.0:${PORT}/api/chat (original chat)`);
    console.log(`  - GET  http://0.0.0.0:${PORT}/api/agent-stream (SSE for UI)`);
    console.log(`  - POST http://0.0.0.0:${PORT}/api/agent-message (receive from Dline)`);
});
