import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [':host { display: block; height: 100%; }']
})
export class App {
  // Eagerly initialise so the effect applies data-theme at startup
  constructor() { inject(ThemeService); }
}
