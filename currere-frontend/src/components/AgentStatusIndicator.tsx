import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAgentStore } from '../store/useAgentStore';

export const AgentStatusIndicator: React.FC = () => {
  const { isAgentActive, currentStatus } = useAgentStore();

  return (
    <AnimatePresence>
      {isAgentActive && (
        <motion.div
          initial={{ opacity: 0, y: 10, scale: 0.98 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: -10, scale: 0.98 }}
          transition={{ duration: 0.4, ease: [0.23, 1, 0.32, 1] }} // Apple-style smooth easing
          className="w-full flex items-center h-8 px-3 rounded-full bg-slate-50/50 dark:bg-slate-800/30 backdrop-blur-xl border border-slate-200/50 dark:border-slate-700/50 shadow-sm"
        >
          {/* Soft Pulsing Dot */}
          <div className="relative flex h-2 w-2 mr-3 items-center justify-center">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-slate-400/60 dark:bg-slate-300/60 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-slate-500 dark:bg-slate-400"></span>
          </div>

          {/* Smooth Text Transition (Blur + Fade) */}
          <div className="flex-1 relative h-5 overflow-hidden flex items-center">
            <AnimatePresence mode="wait">
              <motion.span
                key={currentStatus}
                initial={{ opacity: 0, filter: 'blur(4px)', y: 3 }}
                animate={{ opacity: 1, filter: 'blur(0px)', y: 0 }}
                exit={{ opacity: 0, filter: 'blur(2px)', y: -3 }}
                transition={{ duration: 0.25, ease: 'easeOut' }}
                className="absolute text-[13px] font-medium tracking-tight text-slate-500 dark:text-slate-400 truncate w-full"
              >
                {currentStatus || 'Sistem hazırlanıyor...'}
              </motion.span>
            </AnimatePresence>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
};
