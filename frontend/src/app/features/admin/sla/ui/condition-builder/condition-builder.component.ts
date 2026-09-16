import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { ConditionOperator, ConditionRow } from '../../data-access/interfaces/sla-policy.interface';

const OPERATORS: ConditionOperator[] = [
  'Equals', 'NotEquals', 'In', 'NotIn', 'Contains', 'GreaterThan', 'LessThan', 'IsNull', 'IsNotNull',
];

/**
 * Rows of field / operator / value, shared by the SLA policy, assignment rule and escalation rule
 * editors — the three places conditions are authored against the same server-side allow-list
 * (`IConditionEvaluator`). The field list is passed in from the server rather than hard-coded, so
 * the picker can never drift from what the backend actually evaluates.
 */
@Component({
  selector: 'app-condition-builder',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './condition-builder.component.html',
})
export class ConditionBuilderComponent {
  readonly fields = input<string[]>([]);
  readonly rows = input<ConditionRow[]>([]);
  readonly rowsChange = output<ConditionRow[]>();

  readonly operators = OPERATORS;

  addRow(): void {
    this.rowsChange.emit([...this.rows(), { field: this.fields()[0] ?? '', operator: 'Equals', value: '' }]);
  }

  updateField(index: number, field: string): void {
    this.#patch(index, { field });
  }

  updateOperator(index: number, operator: string): void {
    this.#patch(index, { operator: operator as ConditionOperator });
  }

  updateValue(index: number, value: string): void {
    this.#patch(index, { value });
  }

  removeRow(index: number): void {
    this.rowsChange.emit(this.rows().filter((_, i) => i !== index));
  }

  needsValue(operator: ConditionOperator): boolean {
    return operator !== 'IsNull' && operator !== 'IsNotNull';
  }

  isListOperator(operator: ConditionOperator): boolean {
    return operator === 'In' || operator === 'NotIn';
  }

  #patch(index: number, patch: Partial<ConditionRow>): void {
    this.rowsChange.emit(this.rows().map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }
}
