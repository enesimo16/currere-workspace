'use client';

import React, { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FiSend, FiX, FiCopy, FiArrowRight, FiCheck, FiCpu, FiActivity } from 'react-icons/fi';
import api from '@/services/api';
import { useWorkspaceStore } from '@/store/useWorkspaceStore';
import ReactMarkdown from 'react-markdown';

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
  const [showFileMenu, setShowFileMenu] = useState(false);
  const [workspaceFiles, setWorkspaceFiles] = useState<{ id: number; fileName: string }[]>([]);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const [injectedIds, setInjectedIds] = useState<Set<string>>(new Set());
  const { 
    activeWorkspace, 
    activeFile, 
    injectCode,
    quotedSnippets,
    referencedFiles,
    removeQuotedSnippet,
    removeReferencedFile,
    clearContext,
    addReferencedFile
  } = useWorkspaceStore();
  type EnginePref = 'auto' | 'groq' | 'ollama';
  const [enginePreference, setEnginePreference] = useState<EnginePref>('auto');

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
    if (!input.trim()) return;

    const userText = input.trim();
    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      type: 'chat',
      text: userText
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsTyping(true);

    try {
      const wsId = activeWorkspace?.id || 0;
      let fileId = 0;

      if (wsId > 0 && activeFile && activeFile.name !== 'main.py') {
        try {
          const fileRes = await api.get(`/workspace/${wsId}/file`);
          fileId = fileRes.data.find((f: { fileName: string; id: number }) => f.fileName === activeFile.name)?.id || 0;
        } catch (err) {
          console.warn('Dosya kimliği alınamadı, genel sohbet moduna geçiliyor.', err);
        }
      }

      const aiRes = await api.post(`/workspace/${wsId}/ai/smart-chat`, {
        prompt: userText,
        fileId: fileId,
        enginePreference: enginePreference,
        quotedSnippets: quotedSnippets,
        referencedFiles: referencedFiles
      });

      // Başarılı gönderimden sonra bağlamı temizle
      clearContext();

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

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const MarkdownComponents: any = {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    code({ inline, className, children, ...props }: any) {
      const match = /language-(\w+)/.exec(className || '');
      const codeString = String(children).replace(/\n$/, '');
      const isBlock = !inline && match;
      
      if (isBlock) {
        return (
          <div className="flex flex-col my-3">
            <div className="flex items-center justify-between px-3 py-1.5 bg-[#1e1e1e] border border-zinc-800/50 border-b-0 rounded-t-md text-xs text-zinc-400">
              <span className="font-medium text-emerald-500/80 uppercase tracking-tighter">{match[1] || 'Code'}</span>
              <div className="flex items-center gap-3">
                <button 
                  onClick={() => copyToClipboard(codeString)} 
                  className="flex items-center gap-1.5 hover:text-emerald-400 transition-colors"
                  title="Kopyala"
                >
                  <FiCopy className="w-3.5 h-3.5" />
                  <span className="text-[10px] font-bold tracking-wider">Kopyala</span>
                </button>
                <button 
                  onClick={() => handleInject(Date.now().toString(), codeString)} 
                  className="flex items-center gap-1.5 hover:text-blue-400 transition-colors"
                  title="IDE'ye Ekle"
                >
                  <FiArrowRight className="w-3.5 h-3.5" />
                  <span className="text-[10px] font-bold tracking-wider">Insert</span>
                </button>
              </div>
            </div>
            <pre className="p-3 bg-[#111111]/90 border border-zinc-800 rounded-b-md overflow-x-auto text-[13px] text-zinc-300 font-mono">
              <code className={className} {...props}>
                {children}
              </code>
            </pre>
          </div>
        );
      }
      return (
        <code className="px-1.5 py-0.5 rounded-md bg-black/30 text-emerald-300 font-mono text-[13px]" {...props}>
          {children}
        </code>
      );
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

              <div 
                className="flex-1 overflow-y-auto p-4 space-y-4 custom-scrollbar"
                onPointerDownCapture={(e) => e.stopPropagation()}
              >
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
                            : 'bg-[#2d2d2d]/80 text-zinc-200 border border-zinc-700/30 rounded-bl-sm px-4 py-2.5 w-full'
                      }`}
                    >
                      {msg.sender === 'user' || msg.type === 'error' ? (
                        <div className="whitespace-pre-wrap">{msg.text}</div>
                      ) : (
                        <div className="text-zinc-200 space-y-2">
                          <ReactMarkdown components={MarkdownComponents}>
                            {msg.type === 'code' && !msg.text.includes('```') ? `\`\`\`python\n${msg.text}\n\`\`\`` : msg.text}
                          </ReactMarkdown>
                        </div>
                      )}
                    </div>
                  </div>
                ))}
                {isTyping && (
                   <div className="text-[10px] text-emerald-500/80 animate-pulse font-black tracking-widest pl-2">AI IS THINKING...</div>
                )}
                <div ref={messagesEndRef} />
              </div>

              <div 
                className="p-3 bg-[#111111]/95 border-t border-zinc-800/50 backdrop-blur-md flex flex-col gap-2"
                onPointerDownCapture={(e) => e.stopPropagation()}
              >
                {/* Segmented Control for Engine Selection */}
                <div className="flex items-center justify-between bg-zinc-900/80 p-1 rounded-lg border border-zinc-800/50">
                  <button 
                    onClick={() => setEnginePreference('auto')}
                    className={`flex-1 text-[10px] font-medium py-1.5 rounded-md transition-all ${enginePreference === 'auto' ? 'bg-zinc-800 text-zinc-200 shadow-sm' : 'text-zinc-500 hover:text-zinc-300'}`}
                  >
                    Auto (Hybrid)
                  </button>
                  <button 
                    onClick={() => setEnginePreference('groq')}
                    className={`flex-1 text-[10px] font-medium py-1.5 rounded-md transition-all ${enginePreference === 'groq' ? 'bg-zinc-800 text-zinc-200 shadow-sm' : 'text-zinc-500 hover:text-zinc-300'}`}
                  >
                    Cloud (Groq)
                  </button>
                  <button 
                    onClick={() => setEnginePreference('ollama')}
                    className={`flex-1 text-[10px] font-medium py-1.5 rounded-md transition-all ${enginePreference === 'ollama' ? 'bg-zinc-800 text-zinc-200 shadow-sm' : 'text-zinc-500 hover:text-zinc-300'}`}
                  >
                    Local (Ollama)
                  </button>
                </div>

                {/* Context Zone */}
                {(quotedSnippets.length > 0 || referencedFiles.length > 0) && (
                  <div className="flex flex-wrap gap-2 p-2 bg-zinc-900/60 border border-zinc-800/50 rounded-lg max-h-32 overflow-y-auto custom-scrollbar">
                    {referencedFiles.map(file => (
                      <div key={file} className="flex items-center gap-1.5 bg-zinc-800 text-zinc-300 px-2 py-1 rounded border border-zinc-700 text-[10px]">
                        <span>📄 {file}</span>
                        <button onClick={() => removeReferencedFile(file)} className="hover:text-red-400 ml-1"><FiX className="w-3 h-3" /></button>
                      </div>
                    ))}
                    {quotedSnippets.map(snippet => (
                      <div key={snippet.id} className={`flex items-center gap-1.5 px-2 py-1 rounded border border-zinc-700 bg-zinc-800 text-zinc-300 text-[10px] border-l-2 ${snippet.type === 'terminal' ? 'border-l-red-500' : 'border-l-blue-500'}`}>
                        <span className="truncate max-w-[150px]">{snippet.type === 'terminal' ? 'Terminal' : 'Code'}: {snippet.content}</span>
                        <button onClick={() => removeQuotedSnippet(snippet.id)} className="hover:text-red-400 ml-1"><FiX className="w-3 h-3" /></button>
                      </div>
                    ))}
                  </div>
                )}

                <div className="relative flex items-center">
                  {showFileMenu && (
                    <div className="absolute left-2 bottom-full mb-2 w-48 bg-[#1a1a1a] border border-zinc-700/50 rounded-lg shadow-2xl overflow-hidden z-20">
                      <div className="p-2 border-b border-zinc-800 text-[10px] text-zinc-500 font-bold uppercase tracking-wider">
                        BAĞLAMA DOSYA EKLE
                        <button onClick={() => setShowFileMenu(false)} className="float-right hover:text-zinc-300">✕</button>
                      </div>
                      <div className="max-h-32 overflow-y-auto custom-scrollbar">
                        {workspaceFiles.length === 0 ? (
                          <div className="p-3 text-xs text-zinc-500 text-center">Dosya bulunamadı.</div>
                        ) : (
                          workspaceFiles.map(f => (
                            <button 
                              key={f.id}
                              onClick={() => {
                                addReferencedFile(f.fileName);
                                setShowFileMenu(false);
                              }}
                              className="w-full text-left px-3 py-2 text-xs text-zinc-300 hover:bg-emerald-500/20 hover:text-emerald-400 transition-colors"
                            >
                              📄 {f.fileName}
                            </button>
                          ))
                        )}
                      </div>
                    </div>
                  )}

                  <button 
                    onClick={async () => {
                       if (showFileMenu) {
                         setShowFileMenu(false);
                         return;
                       }
                       if (!activeWorkspace?.id) return;
                       try {
                         const res = await api.get(`/workspace/${activeWorkspace.id}/file`);
                         setWorkspaceFiles(res.data);
                         setShowFileMenu(true);
                       } catch(e) {
                         console.error(e);
                       }
                    }}
                    className="absolute left-2 p-1.5 text-zinc-400 hover:text-emerald-400 transition-colors z-10 bg-zinc-800/50 rounded-lg"
                    title="Bağlama Dosya Ekle"
                  >
                    <span className="text-lg font-bold leading-none">+</span>
                  </button>
                  <input 
                    type="text" 
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    disabled={isTyping}
                    placeholder="Ask about your code (Type @ for files)..."
                    className="w-full bg-[#1a1a1a] text-zinc-200 text-xs rounded-xl pl-9 pr-10 py-3 outline-none border border-zinc-800 focus:border-emerald-500/40 transition-all placeholder:opacity-30"
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
