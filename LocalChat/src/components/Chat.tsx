import React, { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import './Chat.css';
// @ts-ignore
import ChatInput from '../../components/ChatInput.jsx';

interface Message {
  id: string;
  content: string;
  role: 'user' | 'assistant' | 'function';
  timestamp: Date;
  isFunction?: boolean;
  functionName?: string;
  functionStatus?: 'running' | 'completed';
  includeInHistory?: boolean; // Controls whether this message is sent to AI
}

const Chat: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Your service endpoint
  const API_ENDPOINT = 'http://localhost:5991';

  // Log when component loads
  console.log('Chat component loaded. API endpoint:', API_ENDPOINT);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const sendMessage = async (content: string) => {
    console.log('=== SEND MESSAGE STARTED ===');
    console.log('Input value:', content);
    console.log('Is loading:', isLoading);
    
    if (!content.trim() || isLoading) {
      console.log('Returning early - empty input or loading');
      return;
    }

    const userMessage: Message = {
      id: Date.now().toString(),
      content: content.trim(),
      role: 'user',
      timestamp: new Date(),
      includeInHistory: true
    };

    console.log('Created user message:', userMessage);
    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    console.log('UI state updated, starting SSE connection...');

    try {
      // Get current conversation history (excluding function messages)
      const currentMessages = [...messages, userMessage];
      
      // Prepare the request payload with conversation history (exclude function messages)
      const requestPayload = {
        messages: currentMessages
          .filter(msg => msg.includeInHistory !== false) // Include messages unless explicitly excluded
          .map(msg => ({
            role: msg.role === 'function' ? 'assistant' : msg.role, // Convert function role to assistant for API
            content: msg.content
          }))
      };

      // Log the message structure being sent to the agent
      console.log('Sending message to agent via SSE:', {
        endpoint: `${API_ENDPOINT}/nextEvent`,
        method: 'POST',
        payload: requestPayload,
        timestamp: new Date().toISOString()
      });

      // First, send the POST request to initiate the conversation
      const response = await fetch(`${API_ENDPOINT}/nextEvent`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-App-Name': 'standalone-chat',
          'Accept': 'text/event-stream',
        },
        body: JSON.stringify(requestPayload),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      console.log('SSE connection established, processing stream...');

      // Process the streaming response
      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (!reader) {
        throw new Error('No response body reader available');
      }

      let buffer = '';
      let messageProcessed = false;

      try {
        while (true) {
          const { done, value } = await reader.read();
          
          if (done) {
            console.log('🏁 Stream ended');
            break;
          }

          // Decode the chunk and add to buffer
          buffer += decoder.decode(value, { stream: true });
          
          // Process complete lines
          const lines = buffer.split('\n');
          buffer = lines.pop() || ''; // Keep incomplete line in buffer

          for (const line of lines) {
            const trimmedLine = line.trim();
            
            if (!trimmedLine) {
              continue; // Skip empty lines
            }

            console.log('Processing SSE line:', trimmedLine);

            // Handle end of stream
            if (trimmedLine === 'data: [DONE]') {
              console.log('🏁 End of stream detected - server is done sending messages');
              continue;
            }

            // Handle text events (progress messages)
            if (trimmedLine.startsWith('event: text')) {
              continue; // The data will be on the next line
            }

            // Handle function events
            if (trimmedLine.startsWith('event: function')) {
              continue; // The data will be on the next line
            }

            if (trimmedLine.startsWith('data: ')) {
              const dataContent = trimmedLine.substring(6); // Remove "data: " prefix
              
              // Handle function events
              if (dataContent.startsWith('[START] ')) {
                const functionName = dataContent.substring(8); // Remove "[START] " prefix
                console.log('Function started:', functionName);
                
                const functionMessage: Message = {
                  id: (Date.now() + Math.random()).toString(),
                  content: functionName,
                  role: 'function',
                  timestamp: new Date(),
                  isFunction: true,
                  functionName: functionName,
                  functionStatus: 'running',
                  includeInHistory: false // Don't include in AI conversation history
                };
                
                setMessages(prev => [...prev, functionMessage]);
                continue;
              }
              
              if (dataContent === '[END]') {
                console.log('Function ended');
                
                // Find the most recent running function and mark it as completed
                setMessages(prev => {
                  let functionUpdated = false;
                  const updatedMessages = [...prev].reverse().map(msg => {
                    if (!functionUpdated && msg.isFunction && msg.functionStatus === 'running') {
                      functionUpdated = true;
                      return { ...msg, functionStatus: 'completed' as const };
                    }
                    return msg;
                  }).reverse();
                  return updatedMessages;
                });
                
                // Keep completed function messages visible (don't remove them)
                continue;
              }
              
              // Try to parse as JSON first (for assistant messages)
              try {
                const messageData = JSON.parse(dataContent);
                console.log('Parsed assistant message:', messageData);
                
                const assistantMessage: Message = {
                  id: (Date.now() + Math.random()).toString(),
                  content: messageData.message || 'No response received',
                  role: (messageData.role as 'user' | 'assistant') || 'assistant',
                  timestamp: new Date(),
                  includeInHistory: true
                };

                setMessages(prev => [...prev, assistantMessage]);
                messageProcessed = true;
                
                console.log('✅ Added assistant message to conversation:', {
                  messageId: assistantMessage.id,
                  content: assistantMessage.content,
                  role: assistantMessage.role
                });
              } catch (parseError) {
                // If JSON parsing fails, treat as plain text (progress message)
                console.log('Displaying progress text:', dataContent);
                
                // Add progress message as temporary assistant message
                const progressMessage: Message = {
                  id: `progress-${Date.now() + Math.random()}`,
                  content: `🔄 ${dataContent}`,
                  role: 'assistant',
                  timestamp: new Date(),
                  includeInHistory: true
                };

                setMessages(prev => [...prev, progressMessage]);
                console.log('✅ Added progress message:', dataContent);
              }
            }

            // Handle pure JSON lines (fallback for direct JSON)
            else if (trimmedLine.startsWith('{') && trimmedLine.endsWith('}')) {
              try {
                const messageData = JSON.parse(trimmedLine);
                console.log('Parsed pure JSON message:', messageData);
                
                const assistantMessage: Message = {
                  id: (Date.now() + Math.random()).toString(),
                  content: messageData.message || 'No response received',
                  role: (messageData.role as 'user' | 'assistant') || 'assistant',
                  timestamp: new Date(),
                  includeInHistory: true
                };

                setMessages(prev => [...prev, assistantMessage]);
                messageProcessed = true;
                
                console.log('✅ Added JSON message to conversation:', {
                  messageId: assistantMessage.id,
                  content: assistantMessage.content,
                  role: assistantMessage.role
                });
              } catch (parseError) {
                console.error('Error parsing pure JSON line:', parseError, 'from line:', trimmedLine);
              }
            }
          }
        }
      } finally {
        reader.releaseLock();
      }

      if (!messageProcessed) {
        console.warn('No final message was processed from SSE stream');
        // Add a fallback message
        const fallbackMessage: Message = {
          id: (Date.now() + 1).toString(),
          content: 'Processing completed',
          role: 'assistant',
          timestamp: new Date(),
          includeInHistory: true
        };
        setMessages(prev => [...prev, fallbackMessage]);
      }

    } catch (error) {
      console.log('=== ERROR OCCURRED ===');
      console.error('Full error object:', error);
      
      // Log detailed error information
      console.error('Error sending message to agent:', {
        error: error,
        timestamp: new Date().toISOString(),
        endpoint: `${API_ENDPOINT}/nextEvent`,
        userMessage: userMessage.content
      });

      let errorMessage = 'Failed to send message';
      if (error instanceof TypeError && error.message.includes('Failed to fetch')) {
        errorMessage = 'Could not connect to the service. Make sure your service is running on localhost:5991';
      } else if (error instanceof Error) {
        errorMessage = error.message;
      }

      const errorMsg: Message = {
        id: (Date.now() + 1).toString(),
        content: `Error: ${errorMessage}`,
        role: 'assistant',
        timestamp: new Date(),
        includeInHistory: true
      };

      setMessages(prev => [...prev, errorMsg]);
    } finally {
      setIsLoading(false);
      console.log('=== SEND MESSAGE COMPLETED ===');
    }
  };

  const clearChat = () => {
    setMessages([]);
  };

  // Extract user message history for input navigation
  const userHistory = messages.filter(m => m.role === 'user').map(m => m.content);

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
          message.isFunction ? (
            <div key={message.id} className="function-indicator">
              <div className="function-content">
                {message.functionStatus === 'running' ? (
                  <div className="function-spinner"></div>
                ) : (
                  <div className="function-check">✅</div>
                )}
                <span className="function-name">{message.functionName}</span>
              </div>
              <div className="message-timestamp">
                {message.timestamp.toLocaleTimeString()}
              </div>
            </div>
          ) : (
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
          )
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
        <ChatInput onSend={sendMessage} history={userHistory} />
      </div>
    </div>
  );
};

export default Chat;
