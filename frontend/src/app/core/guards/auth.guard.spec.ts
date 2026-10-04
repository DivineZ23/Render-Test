import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PageAccessService } from '../services/page-access.service';
import { authGuard, managerGuard, pageAccessGuard } from './auth.guard';

describe('route guards', () => {
  const run = (guard: typeof authGuard, route: object = {}) =>
    TestBed.runInInjectionContext(() => guard(route as never, {} as never));

  it('routes pending users to approval status', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            loadCurrentUser: () =>
              of({ role: 'agent', approvalStatus: 'pending', accessStatus: 'pending' }),
          },
        },
      ],
    });
    const result = (await firstValueFrom(run(authGuard) as never)) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/pending-approval');
  });

  it('blocks agents from manager routes', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            loadCurrentUser: () =>
              of({ role: 'agent', approvalStatus: 'approved', accessStatus: 'active' }),
          },
        },
      ],
    });
    const result = (await firstValueFrom(run(managerGuard) as never)) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/dashboard');
  });

  it('allows owners through manager routes', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            loadCurrentUser: () =>
              of({ role: 'owner', approvalStatus: 'approved', accessStatus: 'active' }),
          },
        },
      ],
    });
    expect(await firstValueFrom(run(managerGuard) as never)).toBe(true);
  });

  it('requires both parent and child access for nested pages', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PageAccessService,
          useValue: {
            load: () => of({ permissions: {} }),
            canAccess: (resource: string) => resource === 'portfolio.properties.sell',
            firstAccessibleUrl: () => '/dashboard',
          },
        },
      ],
    });

    const result = (await firstValueFrom(
      run(pageAccessGuard, {
        data: {
          accessKey: 'portfolio.properties.sell',
          parentAccessKey: 'portfolio.properties',
        },
      }) as never,
    )) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/dashboard');
  });

  it('allows a nested page when both permissions are enabled', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PageAccessService,
          useValue: {
            load: () => of({ permissions: {} }),
            canAccess: () => true,
          },
        },
      ],
    });

    expect(
      await firstValueFrom(
        run(pageAccessGuard, {
          data: {
            accessKey: 'portfolio.properties.sell',
            parentAccessKey: 'portfolio.properties',
          },
        }) as never,
      ),
    ).toBe(true);
  });
});
