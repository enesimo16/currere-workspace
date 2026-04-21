'use client';

import React, { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FiSend, FiX, FiCopy, FiArrowRight, FiCheck, FiCpu, FiActivity } from 'react-icons/fi';
import api from '@/services/api';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';

interface Message {
  id: string;
  sender: 'user' | 'ai';
  type: 'chat' | 'code' | 'error';
  text: string;
}

export default function CurrereAI() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([
    { id: '1', sender: 'ai', type: 'chat', text: 'Merhaba! Ben Currere AI. Veri dosyanızla ilgili nasıl yardımcı olabilirim?' }
  ]);
  const [input, setInput] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const { activeWorkspace, activeFile, injectCode } = useWorkspaceStore();
  const [injectedIds, setInjectedIds] = useState<Set<string>>(new Set());

  const dragConstraintsRef = useRef(null);

  const [dragStartPos, setDragStartPos] = useState({ x: 0, y: 0 });

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) {
      scrollToBottom();
    }
  }, [messages, isOpen, isTyping]);

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text.replace(/```python\s*|```/g, ''));
  };

  const handleInject = (id: string, code: string) => {
    const cleanCode = code.replace(/```python\s*|```/g, '');
    injectCode(cleanCode);
    setInjectedIds(prev => new Set(prev).add(id));
    
    // Reset success state after 2 seconds
    setTimeout(() => {
      setInjectedIds(prev => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
    }, 2000);
  };

  const handleSend = async () => {
    if (!input.trim() || !activeWorkspace) return;

    const userText = input.trim();
    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      type: 'chat',
      text: userText
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    
    // Eğer main.py açıksa veri dosyası yok demektir
    if (!activeFile || activeFile.name === 'main.py') {
      setMessages(prev => [...prev, {
        id: (Date.now() + 1).toString(),
        sender: 'ai',
        type: 'error',
        text: 'Lütfen analiz edilecek bir dosya (CSV, JSON vb.) seçin.'
      }]);
      return;
    }

    setIsTyping(true);

    try {
      // Dosya ID'sini bul (FileExplorer'a bağımlı kalmadan backend'den anlık alıyoruz)
      const fileRes = await api.get(`/workspace/${activeWorkspace.id}/file`);
      const fileId = fileRes.data.find((f: { fileName: string; id: number }) => f.fileName === activeFile.name)?.id;

      if (!fileId) {
        throw new Error('Aktif dosya kimliği bulunamadı.');
      }

      // Backend'e gönder
      const aiRes = await api.post(`/workspace/${activeWorkspace.id}/ai/smart-chat`, {
        prompt: userText,
        fileId: fileId
      });

      const responseData = aiRes.data;

      const aiMessage: Message = {
        id: (Date.now() + 2).toString(),
        sender: 'ai',
        type: responseData.type === 'code' ? 'code' : 'chat',
        text: responseData.type === 'code' ? responseData.code : responseData.message
      };

      setMessages(prev => [...prev, aiMessage]);

    } catch (error: unknown) {
      console.error("AI İstek Hatası:", error);
      
      let errorMsg = 'AI motoru ile iletişim kurulamadı.';
      if (error && typeof error === 'object' && 'response' in error) {
        const axErr = error as { response?: { data?: { error?: string } } };
        if (axErr.response?.data?.error) {
          errorMsg = axErr.response.data.error;
        }
      }

      setMessages(prev => [...prev, {
        id: (Date.now() + 3).toString(),
        sender: 'ai',
        type: 'error',
        text: errorMsg
      }]);
    } finally {
      setIsTyping(false);
    }
  };

  return (
    <>
      <div className="fixed inset-0 pointer-events-none" ref={dragConstraintsRef} />

      <motion.div 
        className="fixed bottom-6 right-6 z-50 flex flex-col items-end pointer-events-auto"
        drag
        dragMomentum={false}
      >
        <AnimatePresence>
          {isOpen && (
            <motion.div
              initial={{ opacity: 0, scale: 0.8, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.8, y: 20 }}
              transition={{ duration: 0.2, ease: "easeOut" }}
              className="absolute bottom-20 right-0 w-[340px] md:w-[480px] h-[550px] max-h-[75vh] bg-black/50 backdrop-blur-xl border border-zinc-800/50 rounded-2xl shadow-2xl flex flex-col overflow-hidden pointer-events-auto"
              style={{
                boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255,255,255,0.05) inset'
              }}
            >
              <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800/50 bg-[#1e1e1e]/60">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-full bg-emerald-500/20 flex items-center justify-center border border-emerald-500/30">
                    <FiCpu className="text-emerald-400 w-3.5 h-3.5 animate-pulse" />
                  </div>
                  <span className="font-bold text-zinc-200 text-sm tracking-tight font-sans">Currere AI Assistant</span>
                </div>
                <button 
                  onClick={() => setIsOpen(false)}
                  className="p-1 hover:bg-zinc-800/50 rounded-md text-zinc-500 hover:text-white transition-colors cursor-pointer"
                >
                  <FiX className="w-4 h-4" />
                </button>
              </div>

              <div className="flex-1 overflow-y-auto p-4 space-y-4 custom-scrollbar">
                {messages.map((msg) => (
                  <div 
                    key={msg.id} 
                    className={`flex ${msg.sender === 'user' ? 'justify-end' : 'justify-start'}`}
                  >
                    <div 
                      className={`max-w-[90%] rounded-2xl text-sm leading-relaxed shadow-sm overflow-hidden ${
                        msg.sender === 'user' 
                          ? 'bg-emerald-600/80 text-emerald-50 border border-emerald-500/30 rounded-br-sm px-4 py-2.5' 
                          : msg.type === 'error'
                            ? 'bg-red-500/20 text-red-200 border border-red-500/30 rounded-bl-sm px-4 py-2.5'
                            : msg.type === 'code'
                              ? 'bg-[#111111]/90 border border-zinc-800 rounded-bl-sm w-full'
                              : 'bg-[#2d2d2d]/80 text-zinc-200 border border-zinc-700/30 rounded-bl-sm px-4 py-2.5'
                      }`}
                    >
                      {msg.type === 'code' ? (
                        <div className="flex flex-col">
                          <div className="flex items-center justify-between px-3 py-1.5 bg-[#1e1e1e] border-b border-zinc-800/50 text-xs text-zinc-400">
                            <span className="font-medium text-emerald-500/80 uppercase tracking-tighter">Python</span>
                            <div className="flex items-center gap-3">
                              <button onClick={() => copyToClipboard(msg.text)} className="hover:text-emerald-400 transition-colors"><FiCopy /></button>
                              <button onClick={() => handleInject(msg.id, msg.text)} className="hover:text-blue-400 transition-colors"><FiArrowRight /></button>
                            </div>
                          </div>
                          <pre className="p-3 overflow-x-auto text-[13px] text-zinc-300 font-mono italic">
                            <code>{msg.text.replace(/```python\s*|```/g, '')}</code>
                          </pre>
                        </div>
                      ) : (
                        <span>{msg.text}</span>
                      )}
                    </div>
                  </div>
                ))}
                {isTyping && (
                   <div className="text-[10px] text-emerald-500/80 animate-pulse font-black tracking-widest pl-2">AI IS THINKING...</div>
                )}
                <div ref={messagesEndRef} />
              </div>

              <div className="p-3 bg-[#111111]/95 border-t border-zinc-800/50 backdrop-blur-md">
                <div className="relative flex items-center">
                  <input 
                    type="text" 
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    disabled={isTyping}
                    placeholder="Ask about your code..."
                    className="w-full bg-[#1a1a1a] text-zinc-200 text-xs rounded-xl pl-4 pr-10 py-3 outline-none border border-zinc-800 focus:border-emerald-500/40 transition-all placeholder:opacity-30"
                  />
                  <button 
                    onClick={handleSend}
                    disabled={!input.trim() || isTyping}
                    className="absolute right-2 p-1.5 text-emerald-500 hover:text-emerald-400 transition-colors"
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
          whileHover={{ scale: 1.1, rotate: 5 }}
          whileTap={{ scale: 0.9 }}
          type="button"
          className="w-14 h-14 rounded-full bg-emerald-600 flex items-center justify-center shadow-2xl border border-emerald-400/30 text-white cursor-pointer group relative z-50 pointer-events-auto"
          style={{
            boxShadow: '0 10px 25px -5px rgba(16, 185, 129, 0.4), 0 0 0 1px rgba(255,255,255,0.1) inset'
          }}
        >
          <FiCpu className="w-6 h-6 group-hover:animate-pulse" />
          <div className="absolute inset-0 rounded-full bg-emerald-400/20 animate-ping duration-1000"></div>
        </motion.button>
      </motion.div>
    </>
  );
}
