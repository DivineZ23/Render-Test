import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import {
  catchError,
  concat,
  defer,
  EMPTY,
  finalize,
  Observable,
  of,
  Subject,
  switchMap,
  takeUntil,
  throwError,
} from 'rxjs';

interface SavedModel {
  value: unknown;
  savedAt: number;
  bytes: number;
}

/** Session-memory display models, not a source of truth for forms or permissions. */
@Injectable({ providedIn: 'root' })
export class DataModelService {
  private readonly models = new Map<string, SavedModel>();
  private readonly sessionReset = new Subject<void>();
  private readonly pendingRefreshes = new Set<object>();
  private readonly failures = new Set<string>();
  private bytes = 0;
  private version = 0;
  private readonly refreshingState = signal(false);
  private readonly failedState = signal(false);
  readonly refreshing = this.refreshingState.asReadonly();
  readonly refreshFailed = this.failedState.asReadonly();

  // Each read completes after refreshing; this is not a permanent polling subscription.
  read<T>(key: string, load: (background: boolean) => Observable<T>): Observable<T> {
    return defer(() => {
      const version = this.version;
      const entry = this.models.get(key);
      const age = entry ? Date.now() - entry.savedAt : Infinity;
      const saved = entry && age >= 0 && age < 120_000 ? entry : undefined;
      if (saved) {
        this.models.delete(key);
        this.models.set(key, saved);
        if (age < 10_000 && !this.failures.has(key)) return of(structuredClone(saved.value) as T);
      } else this.remove(key);

      const fresh = defer(() => {
        const token = {};
        if (saved) {
          this.pendingRefreshes.add(token);
          this.refreshingState.set(true);
        }
        return load(!!saved).pipe(
          switchMap((value) => {
            // A write finished during this read: do not publish its old response.
            if (version !== this.version) return this.read(key, load);
            this.remember(key, value);
            this.failures.delete(key);
            this.failedState.set(this.failures.size > 0);
            return of(structuredClone(value));
          }),
          catchError((error: HttpErrorResponse) => {
            if (version === this.version && saved && (error.status === 0 || error.status >= 500)) {
              this.failures.add(key);
              this.failedState.set(true);
              return EMPTY; // Keep the preview with a visible refresh-failure message.
            }
            if (version === this.version) this.remove(key);
            return throwError(() => error);
          }),
          finalize(() => {
            this.pendingRefreshes.delete(token);
            this.refreshingState.set(this.pendingRefreshes.size > 0);
          }),
        );
      });
      return saved ? concat(of(structuredClone(saved.value) as T), fresh) : fresh;
    }).pipe(takeUntil(this.sessionReset));
  }

  invalidate(): void {
    this.version++;
    this.models.clear();
    this.bytes = 0;
    this.failures.clear();
    this.failedState.set(false);
  }

  resetSession(): void {
    this.invalidate();
    this.sessionReset.next();
  }

  private remove(key: string): void {
    const entry = this.models.get(key);
    if (entry) this.bytes -= entry.bytes;
    this.models.delete(key);
    this.failures.delete(key);
    this.failedState.set(this.failures.size > 0);
  }

  private remember(key: string, value: unknown): void {
    const bytes = (JSON.stringify(value)?.length ?? 0) * 2;
    this.remove(key);
    // Skip large exports/images; bound entry count and approximate JSON memory.
    if (bytes > 1024 * 1024) return;
    while (this.models.size >= 80 || this.bytes + bytes > 8 * 1024 * 1024)
      this.remove(this.models.keys().next().value!);
    this.models.set(key, { value: structuredClone(value), savedAt: Date.now(), bytes });
    this.bytes += bytes;
  }
}
