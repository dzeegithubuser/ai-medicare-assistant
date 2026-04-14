import { Component, ChangeDetectionStrategy, input, output, computed } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { DrugSearchResult, DrugDetailAdvanceItem } from '../../../models/drug.model';

export interface DrugSelectionState {
  type: string | null;
  dosageForm: string | null;
  strength: string | null;
}

@Component({
  selector: 'app-drug-selection-panel',
  templateUrl: './drug-selection-panel.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [MatIconModule, MatButtonModule, MatFormFieldModule, MatInputModule],
})
export class DrugSelectionPanelComponent {
  result = input.required<DrugSearchResult>();
  selection = input.required<DrugSelectionState>();
  selectedFormulation = input<DrugDetailAdvanceItem | null>(null);
  quantity = input<number | null>(null);
  confirmed = input<boolean>(false);

  typeSelected = output<string>();
  dosageFormSelected = output<string>();
  strengthSelected = output<string>();
  quantityChanged = output<Event>();
  quantityPresetSelected = output<number>();
  drugConfirmed = output<void>();
  drugEditRequested = output<void>();

  readonly formulations = computed(() => this.result().detail?.drugDetailAdvanceList ?? []);
  readonly hasDetail = computed(() => this.formulations().length > 0);

  readonly availableTypes = computed(() => {
    const types = new Set(this.formulations().map(f => f.drugType).filter(Boolean));
    return Array.from(types).sort();
  });

  readonly availableDosageForms = computed(() => {
    const sel = this.selection();
    return Array.from(new Set(
      this.formulations()
        .filter(f => f.drugType === sel.type)
        .map(f => f.rxnDoseForm || f.newDoseForm)
        .filter(Boolean)
    )).sort();
  });

  readonly availableStrengths = computed(() => {
    const sel = this.selection();
    return Array.from(new Set(
      this.formulations()
        .filter(f => f.drugType === sel.type && (f.rxnDoseForm === sel.dosageForm || f.newDoseForm === sel.dosageForm))
        .map(f => f.strength)
        .filter(Boolean)
    )).sort();
  });

  readonly isReadyToConfirm = computed(() =>
    !!this.selectedFormulation() && !!this.quantity()
  );

  readonly quickQtyOptions = [30, 60, 90];
}
