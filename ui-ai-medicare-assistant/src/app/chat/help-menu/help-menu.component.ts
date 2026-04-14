import { Component, ChangeDetectionStrategy, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';

interface HelpCategory {
  icon: string;
  title: string;
  actions: string[];
}

@Component({
  selector: 'app-help-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatIconModule, MatChipsModule],
  template: `
    <div class="space-y-2.5">
      @for (cat of categories; track cat.title) {
        <div class="bg-white border border-gray-100 rounded-xl p-3">
          <div class="flex items-center gap-2 mb-2">
            <mat-icon class="!text-sm !w-4 !h-4 text-cyan-600">{{ cat.icon }}</mat-icon>
            <span class="text-xs font-semibold text-gray-700">{{ cat.title }}</span>
          </div>
          <div class="flex flex-wrap gap-1.5">
            @for (action of cat.actions; track action) {
              <button
                class="px-2.5 py-1 text-[11px] rounded-full border border-cyan-200 bg-cyan-50 text-cyan-700
                       hover:bg-cyan-100 transition-colors cursor-pointer"
                (click)="actionClicked.emit(action)">
                {{ action }}
              </button>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class HelpMenuComponent {
  actionClicked = output<string>();

  categories: HelpCategory[] = [
    {
      icon: 'bookmark',
      title: 'RECOMMENDATION',
      actions: ['Create recommendation', 'View summary', 'Delete recommendation']
    },
    {
      icon: 'person',
      title: 'PROFILE UPDATES',
      actions: ['Change ZIP code', 'Update date of birth', 'Change health profile', 'Update life expectancy',
                'Change tax filing', 'Update MAGI', 'Toggle concierge']
    },
    {
      icon: 'medication',
      title: 'DRUGS & PHARMACY',
      actions: ['Add a drug', 'Remove a drug', 'Change pharmacy', 'Enable mail-order']
    },
    {
      icon: 'health_and_safety',
      title: 'MEDICARE PLANS',
      actions: ['Compare plans', 'View plan details', 'Check drug coverage']
    },
    {
      icon: 'trending_up',
      title: 'PROJECTIONS & FUNDING',
      actions: ['View lifetime costs', 'View funding requirements']
    }
  ];
}
