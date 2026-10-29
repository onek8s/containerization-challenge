import { useEffect, useState } from 'react';
import { NavLink, Route, Switch, useHistory } from 'react-router-dom';
import { currentUser, storeCodeOf, userManager } from './auth';
import Dashboard from './components/Dashboard';
import Tasks from './components/Tasks';
import Collections from './components/Collections';
import CashUp from './components/CashUp';

function Callback({ onSignedIn }) {
  const history = useHistory();
  const [error, setError] = useState(null);
  useEffect(() => {
    userManager()
      .signinRedirectCallback()
      .then((user) => {
        onSignedIn(user);
        history.replace(user.state || '/');
      })
      .catch((e) => setError(e.message));
  }, [history, onSignedIn]);
  return <p>{error ? 'Sign-in failed: ' + error : 'Signing in…'}</p>;
}

export default function App() {
  const [user, setUser] = useState(undefined);

  useEffect(() => {
    if (window.location.pathname === '/callback') return;
    currentUser().then((u) => {
      if (!u) userManager().signinRedirect({ state: window.location.pathname });
      else setUser(u);
    });
  }, []);

  if (window.location.pathname === '/callback' && !user) {
    return <Callback onSignedIn={setUser} />;
  }
  if (!user) return <p className="loading">Signing in…</p>;

  const store = storeCodeOf(user);
  if (!store) {
    return <p className="error">Your account is not linked to a store. Ask the service desk to set your store code.</p>;
  }

  return (
    <div className="app">
      <header>
        <b>VanArsdel {store}</b>
        <nav>
          <NavLink exact to="/">Today</NavLink>
          <NavLink to="/tasks">Pick</NavLink>
          <NavLink to="/collections">Collect</NavLink>
          <NavLink to="/cash-up">Cash-up</NavLink>
        </nav>
        <button className="link" onClick={() => userManager().signoutRedirect()}>{user.profile.name} ⎋</button>
      </header>
      <main>
        <Switch>
          <Route exact path="/"><Dashboard store={store} /></Route>
          <Route path="/tasks"><Tasks store={store} /></Route>
          <Route path="/collections"><Collections store={store} /></Route>
          <Route path="/cash-up"><CashUp store={store} /></Route>
        </Switch>
      </main>
    </div>
  );
}
