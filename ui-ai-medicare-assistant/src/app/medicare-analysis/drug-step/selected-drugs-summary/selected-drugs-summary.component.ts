import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { ConfirmedDrug } from '../drug-step.component';

@Component({
  selector: 'app-selected-drugs-summary',
  templateUrl: './selected-drugs-summary.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
})
export class SelectedDrugsSummaryComponent {
  confirmedDrugs = input.required<ConfirmedDrug[]>();

  editDrug = output<string>();
  removeDrug = output<string>();
}
