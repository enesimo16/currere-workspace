'use client';

import React, { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { HiSparkles } from 'react-icons/hi';
import { FiSend, FiX } from 'react-icons/fi';

interface Message {
  id: string;
  sender: 'user' | 'ai';
  text: string;
}

export default function CurrereAI() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([
    { id: '1', sender: 'ai', text: 'Merhaba! Ben Currere AI. Kodunuzla ilgili nasıl yardımcı olabilirim?' }
  ]);
  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const dragConstraintsRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) {
      scrollToBottom();
    }
  }, [messages, isOpen]);

  const handleSend = () => {
    if (!input.trim()) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: input.trim()
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');

    // Dummy AI Response
    setTimeout(() => {
      const aiResponse: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'ai',
        text: 'Ben Currere AI, sorunuzu yakında backend\'e bağlandığımda cevaplayacağım!'
      };
      setMessages(prev => [...prev, aiResponse]);
    }, 1000);
  };

  return (
    <>
      {/* Constraints Area for Dragging */}
      <div className="fixed inset-0 pointer-events-none" ref={dragConstraintsRef} />

      <motion.div 
        className="fixed bottom-6 right-6 z-50 flex flex-col items-end pointer-events-auto"
        drag
        dragConstraints={dragConstraintsRef}
        dragMomentum={false}
      >
        <AnimatePresence>
          {isOpen && (
            <motion.div
              initial={{ opacity: 0, scale: 0.8, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.8, y: 20 }}
              transition={{ duration: 0.2, ease: "easeOut" }}
              className="mb-4 w-[340px] md:w-[400px] h-[500px] max-h-[70vh] bg-black/40 backdrop-blur-md border border-gray-700/50 rounded-2xl shadow-2xl flex flex-col overflow-hidden"
              style={{
                boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255,255,255,0.05) inset'
              }}
            >
              {/* Chat Header */}
              <div className="flex items-center justify-between px-4 py-3 border-b border-gray-700/50 bg-[#1e1e1e]/60">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-full bg-emerald-500/20 flex items-center justify-center border border-emerald-500/30">
                    <HiSparkles className="text-emerald-400 w-3.5 h-3.5" />
                  </div>
                  <span className="font-semibold text-zinc-200 text-sm tracking-wide">Currere AI</span>
                </div>
                <button 
                  onClick={() => setIsOpen(false)}
                  className="p-1 hover:bg-gray-700/50 rounded-md text-gray-400 hover:text-white transition-colors cursor-pointer"
                >
                  <FiX className="w-4 h-4" />
                </button>
              </div>

              {/* Chat Messages */}
              <div className="flex-1 overflow-y-auto p-4 space-y-4 custom-scrollbar">
                {messages.map((msg) => (
                  <div 
                    key={msg.id} 
                    className={`flex ${msg.sender === 'user' ? 'justify-end' : 'justify-start'}`}
                  >
                    <div 
                      className={`max-w-[85%] rounded-2xl px-4 py-2.5 text-sm leading-relaxed shadow-sm ${
                        msg.sender === 'user' 
                          ? 'bg-emerald-600/80 text-emerald-50 border border-emerald-500/30 rounded-br-sm' 
                          : 'bg-[#2d2d2d]/80 text-zinc-200 border border-gray-600/30 rounded-bl-sm'
                      }`}
                    >
                      {msg.text}
                    </div>
                  </div>
                ))}
                <div ref={messagesEndRef} />
              </div>

              {/* Chat Input */}
              <div className="p-3 bg-[#1e1e1e]/80 border-t border-gray-700/50">
                <div className="relative flex items-center">
                  <input 
                    type="text" 
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    placeholder="Kodunuzla ilgili bir soru sorun..."
                    className="w-full bg-[#111111]/80 text-zinc-200 text-sm rounded-xl pl-4 pr-10 py-3 outline-none border border-gray-700/50 focus:border-emerald-500/50 transition-colors placeholder:text-zinc-500"
                  />
                  <button 
                    onClick={handleSend}
                    disabled={!input.trim()}
                    className="absolute right-2 p-1.5 rounded-lg bg-emerald-500/20 text-emerald-400 hover:bg-emerald-500/30 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    <FiSend className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Floating Bubble Button */}
        <motion.button
          onClick={() => setIsOpen(!isOpen)}
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="w-14 h-14 rounded-full bg-emerald-600 flex items-center justify-center shadow-xl border border-emerald-400/30 text-white cursor-pointer group"
          style={{
            boxShadow: '0 10px 25px -5px rgba(16, 185, 129, 0.4), 0 0 0 1px rgba(255,255,255,0.1) inset'
          }}
        >
          <HiSparkles className="w-6 h-6 group-hover:animate-pulse" />
        </motion.button>
      </motion.div>
    </>
  );
}
