import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PageAccessService } from '../services/page-access.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.loadCurrentUser().pipe(
    map((user) => {
      if (!user) return router.createUrlTree(['/']);
      if (user.approvalStatus === 'pending' || user.accessStatus === 'pending')
        return router.createUrlTree(['/pending-approval']);
      if (user.approvalStatus === 'rejected' || user.accessStatus === 'revoked')
        return router.createUrlTree(['/access-revoked']);
      return true;
    }),
  );
};

export const managerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .loadCurrentUser()
    .pipe(
      map((user) =>
        (user?.role === 'manager' || user?.role === 'owner') &&
        user.approvalStatus === 'approved' &&
        user.accessStatus === 'active'
          ? true
          : router.createUrlTree(['/dashboard']),
      ),
    );
};

export const pageAccessGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const access = inject(PageAccessService);
  const router = inject(Router);
  const resource = route.data['accessKey'] as string | undefined;
  const parentResource = route.data['parentAccessKey'] as string | undefined;
  if (!resource) return true;
  return access
    .load()
    .pipe(
      map(() =>
        access.canAccess(resource) && (!parentResource || access.canAccess(parentResource))
          ? true
          : router.parseUrl(access.firstAccessibleUrl()),
      ),
    );
};
