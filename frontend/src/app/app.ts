import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppFooter } from './shared/app-footer/app-footer';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppFooter],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
