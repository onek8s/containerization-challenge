import { useEffect, useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import { api } from "../api";

export function Layout() {
  const [me, setMe] = useState<{ name: string; roles: string[] }>();
  useEffect(() => {
    api.get<{ name: string; roles: string[] }>("/api/me").then(setMe);
  }, []);

  return (
    <>
      <header>
        <Link to="/" className="brand">Study Data <span>Monitoring</span></Link>
        <nav>
          <NavLink to="/" end>Studies</NavLink>
          {me?.roles.some(r => r === "MedicalCoder" || r === "DataManager") && <NavLink to="/coding">Coding</NavLink>}
        </nav>
        <span className="who">{me?.name}</span>
      </header>
      <main>
        <Outlet />
      </main>
    </>
  );
}

export function useLoad<T>(load: () => Promise<T>, deps: unknown[]): [T | undefined, string | undefined, () => void] {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<string>();
  const [version, setVersion] = useState(0);
  useEffect(() => {
    let active = true;
    setError(undefined);
    load().then(d => active && setData(d)).catch(e => active && setError(String(e.message ?? e)));
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version]);
  return [data, error, () => setVersion(v => v + 1)];
}

export function Pill({ text, tone }: { text: string; tone?: string }) {
  return <span className={`pill ${tone ?? text.toLowerCase()}`}>{text}</span>;
}
