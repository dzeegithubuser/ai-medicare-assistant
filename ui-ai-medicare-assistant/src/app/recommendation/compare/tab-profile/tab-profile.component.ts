import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse } from '../../../models/recommendation.model';
import { buildProfileRows, LABEL_A, LABEL_B } from '../compare-helpers';

@Component({
  selector: 'app-compare-tab-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatCardModule],
  templateUrl: './tab-profile.component.html',
})
export class TabProfileComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

  readonly profileRows = computed(() =>
    buildProfileRows(this.left().profile, this.right().profile));

  readonly personalRows = computed(() => this.profileRows().filter(r => r.group === 'personal'));
  readonly locationRows = computed(() => this.profileRows().filter(r => r.group === 'location'));
  readonly healthRows = computed(() => this.profileRows().filter(r => r.group === 'health'));
  readonly financialRows = computed(() => this.profileRows().filter(r => r.group === 'financial'));

  readonly profileDiffs = computed(() =>
    this.profileRows().filter(r => r.left !== r.right));
}
