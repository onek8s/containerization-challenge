import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { loadAppConfig } from './app/core/app-config';

// The configuration decides how the user signs in, so it is loaded before Angular starts.
loadAppConfig()
  .then(() => platformBrowserDynamic().bootstrapModule(AppModule))
  .catch(err => {
    console.error(err);
    document.body.innerHTML = '<p style="font-family:sans-serif;margin:2em">Expense Claims could not be started. Contact the IT service desk.</p>';
  });
