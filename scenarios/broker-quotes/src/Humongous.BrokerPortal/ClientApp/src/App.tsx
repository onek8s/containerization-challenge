import { createContext, useContext, useEffect, useState } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import type { User } from './api/types';
import { DashboardPage } from './pages/DashboardPage';
import { QuotesPage } from './pages/QuotesPage';
import { QuoteFormPage } from './pages/QuoteFormPage';
import { QuoteDetailPage } from './pages/QuoteDetailPage';
import { PoliciesPage } from './pages/PoliciesPage';
import { PolicyDetailPage } from './pages/PolicyDetailPage';

const UserContext = createContext<User | null>(null);
export const useUser = () => useContext(UserContext)!;

export function App() {
  const [user, setUser] = useState<User | null | undefined>(undefined);

  useEffect(() => {
    fetch('/bff/user', { credentials: 'same-origin' })
      .then(r => (r.ok ? r.json() : null))
      .then(setUser)
      .catch(() => setUser(null));
  }, []);

  if (user === undefined) return <div className="loading">Loading...</div>;
  if (user === null) {
    return (
      <div className="signin">
        <h1>Humongous Insurance broker portal</h1>
        <p>Quote, refer and bind Business Combined cover for your small business clients.</p>
        <a className="button" href={`/bff/login?returnUrl=${encodeURIComponent(window.location.pathname)}`}>Sign in</a>
      </div>
    );
  }
  if (!user.roles.includes('Broker')) {
    return <div className="signin"><h1>No broker access</h1><p>Your account is not a broker account.</p></div>;
  }

  const logout = () =>
    fetch('/bff/logout', { method: 'POST', headers: { 'X-CSRF': '1' }, credentials: 'same-origin', redirect: 'manual' })
      .finally(() => (window.location.href = '/'));

  return (
    <UserContext.Provider value={user}>
      <header className="top">
        <span className="brand">Humongous Insurance</span>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/quotes">Quotes</NavLink>
          <NavLink to="/quotes/new">New quote</NavLink>
          <NavLink to="/policies">Policies</NavLink>
        </nav>
        <span className="who">
          {user.name} ({user.agency}) <button className="link" onClick={logout}>Sign out</button>
        </span>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/quotes" element={<QuotesPage />} />
          <Route path="/quotes/new" element={<QuoteFormPage />} />
          <Route path="/quotes/:id" element={<QuoteDetailPage />} />
          <Route path="/quotes/:id/edit" element={<QuoteFormPage />} />
          <Route path="/policies" element={<PoliciesPage />} />
          <Route path="/policies/:id" element={<PolicyDetailPage />} />
        </Routes>
      </main>
    </UserContext.Provider>
  );
}
