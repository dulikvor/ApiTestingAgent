import React, { useState, useRef, useEffect } from "react";

const ChatInput = ({ onSend, history }) => {
  const [input, setInput] = useState("");
  const [historyIndex, setHistoryIndex] = useState(null);
  const [lastInput, setLastInput] = useState("");
  const inputRef = useRef(null);

  // Always use the latest history for cycling
  const effectiveHistory = history || [];

  // Reset history index if history changes (e.g., after sending a message)
  useEffect(() => {
    setHistoryIndex(null);
  }, [history]);

  const handleKeyDown = (e) => {
    if (e.key === "ArrowUp") {
      if (effectiveHistory.length === 0) return;
      // Only set lastInput when starting to cycle
      if (historyIndex === null) setLastInput(input);
      const newIndex = historyIndex === null ? effectiveHistory.length - 1 : Math.max(0, historyIndex - 1);
      setInput(effectiveHistory[newIndex]);
      setHistoryIndex(newIndex);
      e.preventDefault();
    } else if (e.key === "ArrowDown") {
      if (effectiveHistory.length === 0) return;
      if (historyIndex === null) return;
      const newIndex = historyIndex + 1;
      if (newIndex < effectiveHistory.length) {
        setInput(effectiveHistory[newIndex]);
        setHistoryIndex(newIndex);
      } else {
        setInput(lastInput); // Restore what user was typing before cycling
        setHistoryIndex(null);
      }
      e.preventDefault();
    } else if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      if (input.trim() !== "") {
        onSend(input);
        setInput("");
        setHistoryIndex(null);
        setLastInput("");
        setTimeout(() => inputRef.current?.focus(), 0);
      }
    }
  };

  // When typing, reset historyIndex but do NOT update lastInput (so up restores what you were typing)
  const handleChange = (e) => {
    setInput(e.target.value);
    setHistoryIndex(null);
  };

  return (
    <textarea
      ref={inputRef}
      className="message-input"
      value={input}
      onChange={handleChange}
      onKeyDown={handleKeyDown}
      placeholder="Type your message..."
      autoFocus
      rows={1}
      style={{ resize: "none" }}
    />
  );
};

export default ChatInput;
