"use client";

import {
  createContext,
  useContext,
  useLayoutEffect,
  useState,
  type ReactNode,
} from "react";

const SetExtra = createContext<((node: ReactNode) => void) | null>(null);
const Extra = createContext<ReactNode>(null);

/** Holds page-level topbar actions inside the persistent shell. */
export function PageTopbarProvider({ children }: { children: ReactNode }) {
  const [extra, setExtra] = useState<ReactNode>(null);
  return (
    <SetExtra.Provider value={setExtra}>
      <Extra.Provider value={extra}>{children}</Extra.Provider>
    </SetExtra.Provider>
  );
}

/** Registers extra topbar controls from a page still wrapped in AppShell. */
export function PageTopbar({ children }: { children: ReactNode }) {
  const setExtra = useContext(SetExtra);
  useLayoutEffect(() => {
    if (!setExtra) return;
    setExtra(children);
    return () => setExtra(null);
  }, [children, setExtra]);
  return null;
}

/** Renders the current page's extra topbar actions. */
export function PageTopbarHost() {
  return <>{useContext(Extra)}</>;
}
