import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DrugInteraction } from '../../../models/drug.model';

@Component({
  selector: 'app-interaction-alerts',
  templateUrl: './interaction-alerts.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [MatIconModule],
})
export class InteractionAlertsComponent {
  interactions = input.required<DrugInteraction[]>();

  getSeverityClass(severity: string): string {
    switch (severity) {
      case 'High': return 'bg-red-100 text-red-700 border-red-200';
      case 'Moderate': return 'bg-amber-100 text-amber-700 border-amber-200';
      case 'Low': return 'bg-blue-100 text-blue-700 border-blue-200';
      default: return 'bg-gray-100 text-gray-700 border-gray-200';
    }
  }

  getSeverityIcon(severity: string): string {
    switch (severity) {
      case 'High': return 'error';
      case 'Moderate': return 'warning';
      case 'Low': return 'info';
      default: return 'info';
    }
  }
}
