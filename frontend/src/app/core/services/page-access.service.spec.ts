import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { defaultAccessSettings } from '../constants/access-resource.constants';
import { User } from '../models/user.models';
import { AuthService } from './auth.service';
import { AccessManagementService } from './management.services';
import { PageAccessService } from './page-access.service';

describe('PageAccessService', () => {
  const owner = { role: 'owner' } as User;

  function configure(ownerPermissions: Record<string, boolean>) {
    const settings = defaultAccessSettings();
    for (const [resource, allowed] of Object.entries(ownerPermissions)) {
      settings.permissions[resource] = {
        ...settings.permissions[resource],
        owner: allowed,
      };
    }

    TestBed.configureTestingModule({
      providers: [
        PageAccessService,
        { provide: AuthService, useValue: { user: () => owner } },
        {
          provide: AccessManagementService,
          useValue: {
            get: () => of(settings),
            update: (value: unknown) => of(value),
          },
        },
      ],
    });

    return TestBed.inject(PageAccessService);
  }

  it('honors hidden navigation permissions for the owner', async () => {
    const service = configure({ overview: false });

    await firstValueFrom(service.load());

    expect(service.canAccess('overview')).toBe(false);
    expect(service.canAccess('team')).toBe(true);
  });

  it('falls back to protected access management when every optional owner page is hidden', async () => {
    const settings = defaultAccessSettings();
    const hidden = Object.fromEntries(
      Object.keys(settings.permissions).map((resource) => [resource, false]),
    );
    hidden['administration.accessManagement'] = true;
    const service = configure(hidden);

    await firstValueFrom(service.load());

    expect(service.firstAccessibleUrl()).toBe('/dashboard/access-management');
  });
});
