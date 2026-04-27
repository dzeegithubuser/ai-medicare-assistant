import { Injectable, ElementRef } from '@angular/core';
import {
  Chart, ChartConfiguration, ChartType,
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
} from 'chart.js';

// Register all Chart.js components once — shared across all chart consumers.
Chart.register(
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
);

@Injectable({ providedIn: 'root' })
export class ChartBuilderService {

  /** Create a chart from a signal-based viewChild canvas ref. Returns null if canvas is unavailable. */
  create<T extends ChartType>(
    canvasRef: ElementRef<HTMLCanvasElement> | undefined,
    config: ChartConfiguration<T>,
  ): Chart<T> | null {
    const el = canvasRef?.nativeElement;
    if (!el) return null;
    return new Chart(el, config);
  }

  /** Destroy all charts in the array and clear it. */
  destroyAll(charts: Chart[]): void {
    charts.forEach(c => c.destroy());
    charts.length = 0;
  }
}
