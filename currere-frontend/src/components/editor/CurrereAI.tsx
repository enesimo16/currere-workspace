'use client';

import React, { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { HiSparkles } from 'react-icons/hi';
import { FiSend, FiX, FiCopy } from 'react-icons/fi';
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
  const { activeWorkspace, activeFile } = useWorkspaceStore();

  const dragConstraintsRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) {
      scrollToBottom();
    }
  }, [messages, isOpen, isTyping]);

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
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
              className="mb-4 w-[340px] md:w-[480px] h-[550px] max-h-[75vh] bg-black/50 backdrop-blur-xl border border-gray-700/50 rounded-2xl shadow-2xl flex flex-col overflow-hidden"
              style={{
                boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255,255,255,0.05) inset'
              }}
            >
              <div className="flex items-center justify-between px-4 py-3 border-b border-gray-700/50 bg-[#1e1e1e]/60">
                <div className="flex items-center gap-2">
                  <div className="w-6 h-6 rounded-full bg-emerald-500/20 flex items-center justify-center border border-emerald-500/30">
                    <HiSparkles className="text-emerald-400 w-3.5 h-3.5 animate-pulse" />
                  </div>
                  <span className="font-semibold text-zinc-200 text-sm tracking-wide">Currere AI Asistanı</span>
                </div>
                <button 
                  onClick={() => setIsOpen(false)}
                  className="p-1 hover:bg-gray-700/50 rounded-md text-gray-400 hover:text-white transition-colors cursor-pointer"
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
                              ? 'bg-[#111111]/90 border border-gray-700 rounded-bl-sm w-full'
                              : 'bg-[#2d2d2d]/80 text-zinc-200 border border-gray-600/30 rounded-bl-sm px-4 py-2.5'
                      }`}
                    >
                      {msg.type === 'code' ? (
                        <div className="flex flex-col">
                          <div className="flex items-center justify-between px-3 py-1.5 bg-[#1e1e1e] border-b border-gray-700/50 text-xs text-zinc-400">
                            <span>Python Üretildi</span>
                            <button 
                              onClick={() => copyToClipboard(msg.text)}
                              className="flex items-center gap-1 hover:text-emerald-400 transition-colors"
                              title="Kodu Kopyala"
                            >
                              <FiCopy className="w-3.5 h-3.5" />
                              <span>Kopyala</span>
                            </button>
                          </div>
                          <pre className="p-3 overflow-x-auto text-[13px] text-zinc-300 font-mono">
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
                  <div className="flex justify-start">
                    <div className="max-w-[85%] rounded-2xl px-4 py-3 bg-[#2d2d2d]/80 text-zinc-200 border border-gray-600/30 rounded-bl-sm flex items-center gap-1.5">
                      <span className="text-xs text-emerald-400 font-medium tracking-wider animate-pulse">Currere AI Düşünüyor</span>
                      <div className="flex gap-0.5 mt-1">
                        <div className="w-1 h-1 bg-emerald-500 rounded-full animate-bounce [animation-delay:-0.3s]"></div>
                        <div className="w-1 h-1 bg-emerald-500 rounded-full animate-bounce [animation-delay:-0.15s]"></div>
                        <div className="w-1 h-1 bg-emerald-500 rounded-full animate-bounce"></div>
                      </div>
                    </div>
                  </div>
                )}
                
                <div ref={messagesEndRef} />
              </div>

              <div className="p-3 bg-[#1e1e1e]/80 border-t border-gray-700/50">
                <div className="relative flex items-center">
                  <input 
                    type="text" 
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    disabled={isTyping}
                    placeholder={isTyping ? "Yanıt Bekleniyor..." : "Kodunuzla ilgili bir soru sorun..."}
                    className="w-full bg-[#111111]/80 text-zinc-200 text-sm rounded-xl pl-4 pr-10 py-3 outline-none border border-gray-700/50 focus:border-emerald-500/50 transition-colors placeholder:text-zinc-500 disabled:opacity-50"
                  />
                  <button 
                    onClick={handleSend}
                    disabled={!input.trim() || isTyping}
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
