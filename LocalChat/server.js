const express = require('express');
const cors = require('cors');

const app = express();
const PORT = 5991;

// Middleware
app.use(cors());
app.use(express.json());

// Chat endpoint
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
    res.json({ status: 'healthy' });
});

app.listen(PORT, 'localhost', () => {
    console.log(`Chat server running on http://localhost:${PORT}`);
    console.log('API endpoint: http://localhost:' + PORT + '/api/chat');
});
