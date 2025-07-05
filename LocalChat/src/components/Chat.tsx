import axios from 'axios';
import React, { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import './Chat.css';

interface Message {
  id: string;
  content: string;
  role: 'user' | 'assistant';
  timestamp: Date;
}

interface ChatResponse {
  message: string;
  role: string;
}

const Chat: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isServerProcessing, setIsServerProcessing] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Your service endpoint
  const API_ENDPOINT = 'https://localhost:5991';

  // Log when component loads
  console.log('Chat component loaded. API endpoint:', API_ENDPOINT);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const sendMessage = async () => {
    console.log('=== SEND MESSAGE STARTED ===');
    console.log('Input value:', inputValue);
    console.log('Is loading:', isLoading);
    
    if (!inputValue.trim() || isLoading) {
      console.log('Returning early - empty input or loading');
      return;
    }

    const userMessage: Message = {
      id: Date.now().toString(),
      content: inputValue.trim(),
      role: 'user',
      timestamp: new Date()
    };

    console.log('Created user message:', userMessage);
    setMessages(prev => [...prev, userMessage]);
    setInputValue('');
    setIsLoading(true);
    setIsServerProcessing(true); // Server is now processing
    console.log('UI state updated, starting API call...');

    try {
      // Get current conversation history
      const currentMessages = [...messages, userMessage];
      
      // Prepare the request payload with full conversation history
      const requestPayload = {
        messages: currentMessages.map(msg => ({
          role: msg.role,
          content: msg.content
        }))
      };

      const requestHeaders = {
        'Content-Type': 'application/json',
        'X-App-Name': 'standalone-chat', // Required for server authentication
        // Add any authentication headers if needed
        // 'Authorization': 'Bearer your-token',
      };

      // Log the message structure being sent to the agent
      console.log('Sending message to agent:', {
        endpoint: `${API_ENDPOINT}/nextEvent`,
        method: 'POST',
        headers: requestHeaders,
        payload: requestPayload,
        timestamp: new Date().toISOString()
      });

      // Adjust this request format based on your service's API
      const response = await axios.post<ChatResponse>(`${API_ENDPOINT}/nextEvent`, requestPayload, {
        headers: requestHeaders,
        timeout: 600000, // 10 minute timeout
      });

      // Log the response received from the agent
      console.log('Response received from agent:', {
        status: response.status,
        statusText: response.statusText,
        data: response.data,
        timestamp: new Date().toISOString()
      });

      // Handle combined response with message and [DONE] signal
      const responseData: any = response.data;
      
      // Check if this is a combined SSE-style response
      if (typeof responseData === 'string') {
        console.log('Processing SSE-style response:', responseData);
        const lines = responseData.split('\n');
        console.log('Split into lines:', lines);
        
        let messageProcessed = false;
        
        for (const line of lines) {
          const trimmedLine = line.trim();
          console.log('Processing line:', trimmedLine);
          
          if (trimmedLine === 'data: [DONE]') {
            console.log('🏁 End of stream detected - server is done sending messages');
            setIsServerProcessing(false);
            continue; // Skip this line, don't process as message
          }
          
          // Handle lines that start with "data: " 
          if (trimmedLine.startsWith('data: ')) {
            try {
              const jsonStr = trimmedLine.replace('data: ', '');
              console.log('Extracting JSON from data line:', jsonStr);
              const messageData = JSON.parse(jsonStr);
              console.log('Parsed message data:', messageData);
              
              const assistantMessage: Message = {
                id: (Date.now() + Math.random()).toString(),
                content: messageData.message || 'No response received',
                role: (messageData.role as 'user' | 'assistant') || 'assistant',
                timestamp: new Date()
              };

              setMessages(prev => [...prev, assistantMessage]);
              messageProcessed = true;
              
              console.log('✅ Added message to conversation:', {
                messageId: assistantMessage.id,
                content: assistantMessage.content,
                role: assistantMessage.role
              });
            } catch (parseError) {
              console.error('Error parsing message data from data line:', parseError, 'from line:', trimmedLine);
            }
          }
          // Handle lines that are pure JSON (no "data: " prefix)
          else if (trimmedLine.startsWith('{') && trimmedLine.endsWith('}')) {
            try {
              console.log('Parsing pure JSON line:', trimmedLine);
              const messageData = JSON.parse(trimmedLine);
              console.log('Parsed message data:', messageData);
              
              const assistantMessage: Message = {
                id: (Date.now() + Math.random()).toString(),
                content: messageData.message || 'No response received',
                role: (messageData.role as 'user' | 'assistant') || 'assistant',
                timestamp: new Date()
              };

              setMessages(prev => [...prev, assistantMessage]);
              messageProcessed = true;
              
              console.log('✅ Added message to conversation:', {
                messageId: assistantMessage.id,
                content: assistantMessage.content,
                role: assistantMessage.role
              });
            } catch (parseError) {
              console.error('Error parsing pure JSON line:', parseError, 'from line:', trimmedLine);
            }
          }
        }
        
        if (!messageProcessed) {
          console.warn('No message was processed from SSE response');
        }
        
        return; // Exit early for SSE-style responses
      }

      // Handle regular JSON response (fallback)
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        content: responseData.message || 'No response received',
        role: (responseData.role as 'user' | 'assistant') || 'assistant',
        timestamp: new Date()
      };

      setMessages(prev => [...prev, assistantMessage]);
      
      console.log('✅ Added message to conversation:', {
        messageId: assistantMessage.id,
        content: assistantMessage.content,
        role: assistantMessage.role
      });
    } catch (error) {
      console.log('=== ERROR OCCURRED ===');
      console.error('Full error object:', error);
      
      // Clear server processing state on error
      setIsServerProcessing(false);
      
      // Log detailed error information
      console.error('Error sending message to agent:', {
        error: error,
        timestamp: new Date().toISOString(),
        endpoint: `${API_ENDPOINT}/nextEvent`,
        userMessage: userMessage.content
      });

      console.error('Error sending message:', error);
      
      // Check if it's a network error
      if (axios.isAxiosError(error)) {
        console.log('This is an Axios error');
        console.log('Error code:', error.code);
        console.log('Error message:', error.message);
        console.log('Response status:', error.response?.status);
        console.log('Response data:', error.response?.data);
      } else {
        console.log('This is not an Axios error');
      }
      
      let errorMessage = 'Failed to send message';
      if (axios.isAxiosError(error)) {
        if (error.code === 'ECONNREFUSED') {
          errorMessage = 'Could not connect to the service. Make sure your service is running on localhost:5991';
        } else if (error.response?.status === 404) {
          errorMessage = 'API endpoint not found. Check your service\'s API path';
        } else if (error.response?.data?.message) {
          errorMessage = error.response.data.message;
        }
      }

      const errorMsg: Message = {
        id: (Date.now() + 1).toString(),
        content: `Error: ${errorMessage}`,
        role: 'assistant',
        timestamp: new Date()
      };

      setMessages(prev => [...prev, errorMsg]);
    } finally {
      setIsLoading(false);
      // Note: setIsServerProcessing(false) is only called when [DONE] is received
      console.log('=== SEND MESSAGE COMPLETED ===');
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  const clearChat = () => {
    setMessages([]);
  };

  return (
    <div className="chat-container">
      <div className="chat-header">
        <h2>Chat with Your Agent</h2>
        <button onClick={clearChat} className="clear-button">
          Clear Chat
        </button>
      </div>
      
      <div className="messages-container">
        {messages.length === 0 && (
          <div className="welcome-message">
            <p>Welcome! Start a conversation with your agent running on localhost:5991</p>
            <p className="service-info">Make sure your service is running and accessible at the configured endpoint.</p>
          </div>
        )}
        
        {messages.map((message) => (
          <div
            key={message.id}
            className={`message ${message.role}`}
          >
            <div className="message-content">
              <ReactMarkdown>{message.content}</ReactMarkdown>
            </div>
            <div className="message-timestamp">
              {message.timestamp.toLocaleTimeString()}
            </div>
          </div>
        ))}
        
        {isLoading && (
          <div className="message assistant">
            <div className="message-content">
              <div className="typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          </div>
        )}
        
        <div ref={messagesEndRef} />
      </div>
      
      <div className="input-container">
        <textarea
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Type your message here... (Press Enter to send, Shift+Enter for new line)"
          className="message-input"
          disabled={isLoading || isServerProcessing}
          rows={1}
        />
        <button 
          onClick={sendMessage} 
          disabled={!inputValue.trim() || isLoading || isServerProcessing}
          className="send-button"
        >
          {isLoading ? 'Sending...' : isServerProcessing ? 'Processing...' : 'Send'}
        </button>
      </div>
    </div>
  );
};

export default Chat;
