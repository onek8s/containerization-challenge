import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../core/models';

/** Turns an API error into a message for the user. */
export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    if (problem?.errors) {
      return Object.values(problem.errors).flat().join(' ');
    }
    if (problem?.title) {
      return problem.title;
    }
    if (error.status === 0) {
      return 'The Expense Claims service cannot be reached.';
    }
    return `${error.status} ${error.statusText}`;
  }
  return String(error);
}
