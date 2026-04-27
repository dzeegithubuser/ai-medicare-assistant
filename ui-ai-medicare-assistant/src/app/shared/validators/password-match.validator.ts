import { AbstractControl, ValidationErrors } from '@angular/forms';

export function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPassword = control.get('newPassword');
  const confirmPassword = control.get('confirmPassword');
  if (newPassword && confirmPassword && newPassword.value !== confirmPassword.value) {
    confirmPassword.setErrors({ passwordMismatch: true });
    return { passwordMismatch: true };
  }
  if (confirmPassword?.hasError('passwordMismatch')) {
    confirmPassword.setErrors(null);
  }
  return null;
}
