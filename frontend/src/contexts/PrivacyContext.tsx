import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

interface PrivacyContextValue {
  hidden: boolean;
  toggle: () => void;
}

const PrivacyContext = createContext<PrivacyContextValue>({
  hidden: false,
  toggle: () => {}
});

export function PrivacyProvider({ children }: { children: ReactNode }) {
  const [hidden, setHidden] = useState(() => {
    try {
      return localStorage.getItem('privacy-mode') === '1';
    } catch {
      return false;
    }
  });

  const toggle = useCallback(() => {
    setHidden((prev) => {
      const next = !prev;
      try {
        localStorage.setItem('privacy-mode', next ? '1' : '0');
      } catch {
        // ignore
      }
      return next;
    });
  }, []);

  return (
    <PrivacyContext.Provider value={{ hidden, toggle }}>
      {children}
    </PrivacyContext.Provider>
  );
}

export function usePrivacy() {
  return useContext(PrivacyContext);
}

export function PrivacyMask({ value }: { value: string }) {
  const { hidden } = usePrivacy();
  if (hidden) {
    return <span className="privacy-mask">***</span>;
  }
  return <>{value}</>;
}
