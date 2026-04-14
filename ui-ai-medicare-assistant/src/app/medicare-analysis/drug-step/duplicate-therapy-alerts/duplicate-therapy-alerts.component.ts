import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DuplicateTherapy } from '../../../models/drug.model';

@Component({
  selector: 'app-duplicate-therapy-alerts',
  templateUrl: './duplicate-therapy-alerts.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [MatIconModule],
})
export class DuplicateTherapyAlertsComponent {
  duplicateTherapies = input.required<DuplicateTherapy[]>();
}
