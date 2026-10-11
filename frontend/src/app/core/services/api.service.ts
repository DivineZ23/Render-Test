import {
  HttpClient,
  HttpContext,
  HttpErrorResponse,
  HttpEvent,
  HttpEventType,
  HttpParams,
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, defer, finalize, Observable, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QUIET_HTTP_ERRORS } from '../config/http-context';
import { DataModelService } from './data-model.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly models = inject(DataModelService);
  private readonly baseUrl = environment.apiBaseUrl;
  private readonly pendingReads = new Map<string, Observable<unknown>>();
  private generation = 0;

  // Opt-in display reads may emit saved data, then a background update.
  read<T>(url: string, query?: object): Observable<T> {
    const key = `${this.url(url)}?${this.params(query).toString()}`;
    return this.models.read(key, (background) => this.network<T>(url, query, background));
  }

  clearSavedData(resetSession = false): void {
    this.generation++;
    this.pendingReads.clear();
    if (resetSession) this.models.resetSession();
    else this.models.invalidate();
  }

  // Auth, editable details, permissions and eviction eligibility always read the server.
  get<T>(url: string, query?: object): Observable<T> {
    return this.network(url, query);
  }

  private network<T>(url: string, query?: object, quiet = false): Observable<T> {
    const params = this.params(query);
    const key = `${this.url(url)}?${params.toString()}|${quiet ? 'background' : 'foreground'}`;
    return defer(() => {
      const pending = this.pendingReads.get(key);
      if (pending) return pending as Observable<T>;
      const generation = this.generation;
      const request = this.http
        .get<T>(this.url(url), {
          params,
          context: new HttpContext().set(QUIET_HTTP_ERRORS, quiet),
        })
        .pipe(
          catchError((error: HttpErrorResponse) => {
            if (generation === this.generation && (error.status === 401 || error.status === 403))
              this.clearSavedData(); // Purge models without swallowing the authorization error.
            return throwError(() => error);
          }),
          finalize(() => {
            if (this.pendingReads.get(key) === request) this.pendingReads.delete(key);
          }),
          shareReplay({ bufferSize: 1, refCount: true }),
        );
      this.pendingReads.set(key, request);
      return request;
    });
  }
  post<TRequest, TResponse>(url: string, body: TRequest): Observable<TResponse> {
    return this.mutate(() => this.http.post<TResponse>(this.url(url), body));
  }
  // Read-only POST calculations must not invalidate unrelated page models.
  calculate<TRequest, TResponse>(url: string, body: TRequest): Observable<TResponse> {
    return this.http.post<TResponse>(this.url(url), body);
  }
  put<TRequest, TResponse>(url: string, body: TRequest): Observable<TResponse> {
    return this.mutate(() => this.http.put<TResponse>(this.url(url), body));
  }
  patch<TRequest, TResponse>(url: string, body: TRequest): Observable<TResponse> {
    return this.mutate(() => this.http.patch<TResponse>(this.url(url), body));
  }
  delete<T>(url: string, body?: unknown): Observable<T> {
    return this.mutate(() =>
      this.http.delete<T>(this.url(url), body === undefined ? {} : { body }),
    );
  }
  upload<T>(url: string, file: File): Observable<HttpEvent<T>> {
    const body = new FormData();
    body.append('file', file);
    return this.mutate(
      () => this.http.post<T>(this.url(url), body, { observe: 'events', reportProgress: true }),
      (event) => event.type === HttpEventType.Response,
    );
  }

  private mutate<T>(request: () => Observable<T>, completed = (_: T) => true): Observable<T> {
    return defer(() => {
      this.clearSavedData();
      let invalidated = false;
      const invalidate = () => {
        if (!invalidated) {
          invalidated = true;
          this.clearSavedData();
        }
      };
      return request().pipe(
        tap({
          next: (value) => {
            if (completed(value)) invalidate();
          },
          error: invalidate,
        }),
        finalize(invalidate),
      );
    });
  }
  private url(path: string): string {
    return `${this.baseUrl}${path}`;
  }
  private params(query?: object): HttpParams {
    let params = new HttpParams();
    if (!query) return params;
    for (const [key, value] of Object.entries(query).sort(([left], [right]) =>
      left.localeCompare(right),
    )) {
      if (value === null || value === undefined || value === '') continue;
      if (Array.isArray(value))
        value.forEach((item) => {
          params = params.append(key, item);
        });
      else params = params.set(key, String(value));
    }
    return params;
  }
}
