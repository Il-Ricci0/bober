import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';
import { definePreset } from '@primeng/themes';

import { routes } from './app.routes';

const BlackAura = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fafafa',
      100: '#f5f5f5',
      200: '#e5e5e5',
      300: '#d4d4d4',
      400: '#a3a3a3',
      500: '#737373',
      600: '#525252',
      700: '#404040',
      800: '#262626',
      900: '#171717',
      950: '#0a0a0a'
    },
    colorScheme: {
      light: {
        primary: {
          color: '#000000',
          contrastColor: '#ffffff',
          hoverColor: '#171717',
          activeColor: '#0a0a0a'
        },
        highlight: {
          background: '#000000',
          focusBackground: '#171717',
          color: '#ffffff',
          focusColor: '#ffffff'
        }
      },
      dark: {
        primary: {
          color: '#000000',
          contrastColor: '#ffffff',
          hoverColor: '#171717',
          activeColor: '#0a0a0a'
        },
        highlight: {
          background: '#000000',
          focusBackground: '#171717',
          color: '#ffffff',
          focusColor: '#ffffff'
        }
      }
    }
  }
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: BlackAura,
        options: {
          darkModeSelector: '.app-dark'
        }
      }
    })
  ]
};
