import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, Observable, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../config/api-endpoints';
import { UpdateUserProfileRequest, User } from '../models/user.models';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly state = signal<User | null | undefined>(undefined);
  private loading$?: Observable<User | null>;
  readonly user = this.state.asReadonly();
  readonly isAuthenticated = computed(() => !!this.state());
  readonly isApproved = computed(
    () => this.state()?.approvalStatus === 'approved' && this.state()?.accessStatus === 'active',
  );
  readonly isManager = computed(
    () => this.isApproved() && ['manager', 'owner'].includes(this.state()?.role ?? ''),
  );
  readonly isOwner = computed(() => this.isApproved() && this.state()?.role === 'owner');
  readonly canEvict = computed(
    () =>
      this.isApproved() && ['seniorAgent', 'manager', 'owner'].includes(this.state()?.role ?? ''),
  );

  loadCurrentUser(): Observable<User | null> {
    if (this.state() !== undefined) return of(this.state() ?? null);
    return (this.loading$ ??= this.api.get<User>(API_ENDPOINTS.auth.me).pipe(
      tap((user) => this.state.set(user)),
      catchError(() => {
        this.api.clearSavedData();
        this.state.set(null);
        return of(null);
      }),
      shareReplay(1),
    ));
  }

  signIn(): void {
    this.api.clearSavedData(true);
    window.location.assign(`${environment.apiBaseUrl}${API_ENDPOINTS.auth.signIn}`);
  }
  updateProfile(body: UpdateUserProfileRequest): Observable<User> {
    return this.api
      .put<UpdateUserProfileRequest, User>(API_ENDPOINTS.auth.profile, body)
      .pipe(tap((user) => this.state.set(user)));
  }
  logout(): void {
    this.api.clearSavedData(true);
    this.api.post<null, void>(API_ENDPOINTS.auth.logout, null).subscribe(() => {
      this.state.set(null);
      window.location.assign('/');
    });
  }
  refresh(): void {
    this.api.clearSavedData(true);
    this.state.set(undefined);
    this.loading$ = undefined;
    this.loadCurrentUser().subscribe();
  }
}
