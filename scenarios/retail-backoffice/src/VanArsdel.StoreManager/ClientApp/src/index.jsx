import { render } from 'react-dom';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { loadConfig } from './config';
import './site.css';

loadConfig()
  .then(() =>
    render(
      <BrowserRouter>
        <App />
      </BrowserRouter>,
      document.getElementById('root')
    )
  )
  .catch((e) => {
    document.getElementById('root').innerText = 'Start-up failed: ' + e.message;
  });
