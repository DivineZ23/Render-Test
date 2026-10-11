import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { defer, finalize, Observable, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;
  private readonly pendingReads = new Map<string, Observable<unknown>>();

  get<T>(url: string, query?: object): Observable<T> {
    const params = this.params(query);
    const key = `${this.url(url)}?${params.toString()}`;
    return defer(() => {
      const pending = this.pendingReads.get(key);
      if (pending) return pending as Observable<T>;
      const request = this.http.get<T>(this.url(url), { params }).pipe(
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
    return this.mutate(() =>
      this.http.post<T>(this.url(url), body, { observe: 'events', reportProgress: true }),
    );
  }

  private mutate<T>(request: () => Observable<T>): Observable<T> {
    return defer(() => {
      // Never join a read that started before an update or a session change.
      this.pendingReads.clear();
      return request().pipe(tap(() => this.pendingReads.clear()));
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
