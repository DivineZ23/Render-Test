import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgFor } from '@angular/common';
import { LucideLockKeyhole, LucideShieldCheck } from '@lucide/angular';
import { UserRole } from '../../core/models/user.models';
import { PageAccessService } from '../../core/services/page-access.service';
import { USER_ROLES } from '../../core/constants/user-role.constants';
import {
  ACCESS_SECTIONS,
  AccessResourceDefinition,
  AccessSectionDefinition,
  roleCanOpen,
} from '../../core/constants/access-resource.constants';

@Component({
  selector: 'app-access-management',
  imports: [NgFor, LucideLockKeyhole, LucideShieldCheck],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Administration</p>
        <h1>Access management</h1>
        <p>
          Choose which dashboard pages each role can open. Owners can also tailor their own
          navigation.
        </p>
      </div>
      <span><svg lucideLockKeyhole></svg>Owner only</span>
    </div>
    <div class="panel">
      <div class="intro">
        <svg lucideShieldCheck></svg>
        <div>
          <b>Navigation permissions</b>
          <p>
            Control a complete section at once or fine-tune its individual pages. Restricted roles
            remain protected by server-side authorization. Access Management always stays visible
            to the Owner so these choices can be restored.
          </p>
        </div>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Page</th>
              <th *ngFor="let role of roles">{{ labels[role] }}</th>
            </tr>
          </thead>
          <tbody>
            <ng-container *ngFor="let section of sections">
              <tr class="section-row">
                <td>
                  <b>{{ section.label }}</b>
                  <small>{{ section.resources.length }} pages</small>
                </td>
                <td *ngFor="let role of roles">
                  <label class="toggle section-toggle"
                    ><input
                      type="checkbox"
                      [checked]="sectionAllowed(section, role)"
                      [indeterminate]="sectionPartiallyAllowed(section, role)"
                      [disabled]="!sectionConfigurable(section, role)"
                      (change)="toggleSection(section, role, $any($event.target).checked)"
                    /><span></span><em>{{ sectionState(section, role) }}</em></label
                  >
                </td>
              </tr>
              <tr *ngFor="let resource of section.resources" class="resource-row">
                <td [class.child-resource]="resource.child">
                  <b>{{ resource.label }}</b>
                  <small>{{ resource.child ? 'Property workflow' : 'Page' }}</small>
                </td>
                <td *ngFor="let role of roles">
                  <label class="toggle"
                    ><input
                      type="checkbox"
                      [checked]="allowed(resource.key, role)"
                      [disabled]="!configurable(resource, role)"
                      (change)="toggle(resource.key, role, $any($event.target).checked)"
                    /><span></span><em>{{ resourceState(resource, role) }}</em></label
                  >
                </td>
              </tr>
            </ng-container>
          </tbody>
        </table>
      </div>
    </div>`,
  styles: [
    `
      .page-title {
        display: flex;
        justify-content: space-between;
        align-items: end;
        gap: 20px;
      }
      .page-title h1 {
        font-size: 2.5rem;
        margin: 4px 0;
      }
      .page-title p {
        color: var(--muted);
        margin: 0;
      }
      .page-title > span {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        border-radius: 99px;
        padding: 8px 12px;
        background: var(--forest-light);
        color: var(--forest);
        font-size: 0.78rem;
        font-weight: 750;
      }
      .page-title > span svg {
        width: 15px;
      }
      .panel {
        margin-top: 26px;
        padding: 0;
        overflow: hidden;
      }
      .intro {
        display: flex;
        gap: 13px;
        padding: 20px;
        border-bottom: 1px solid var(--border);
      }
      .intro > svg {
        width: 22px;
        color: var(--forest);
        flex: none;
      }
      .intro b {
        display: block;
      }
      .intro p {
        margin: 4px 0 0;
        color: var(--muted);
        font-size: 0.84rem;
      }
      .table-wrap {
        overflow-x: auto;
      }
      table {
        width: 100%;
        border-collapse: collapse;
        min-width: 780px;
      }
      th,
      td {
        padding: 15px 18px;
        border-bottom: 1px solid var(--border);
        text-align: left;
      }
      th {
        font-size: 0.73rem;
        color: var(--muted);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }
      td:first-child {
        min-width: 210px;
      }
      td small,
      td b {
        display: block;
      }
      td small {
        font-size: 0.72rem;
        color: var(--muted);
        margin-bottom: 2px;
      }
      .section-row td {
        background: color-mix(in srgb, var(--surface-strong) 91%, #64748b 9%);
        border-bottom-color: var(--border);
        padding-top: 17px;
        padding-bottom: 17px;
      }
      .section-row td:first-child {
        box-shadow: inset 3px 0 0 color-mix(in srgb, #64748b 70%, transparent);
      }
      .section-row td:first-child b {
        color: var(--text);
        font-size: 0.88rem;
      }
      .section-row td:first-child small {
        margin-top: 3px;
      }
      .resource-row td:first-child {
        padding-left: 30px;
      }
      .resource-row td:first-child.child-resource {
        padding-left: 48px;
        position: relative;
      }
      .resource-row td:first-child.child-resource::before {
        content: '';
        position: absolute;
        left: 30px;
        top: 50%;
        width: 8px;
        height: 1px;
        background: var(--muted);
      }
      .toggle {
        display: flex;
        align-items: center;
        gap: 8px;
        cursor: pointer;
      }
      .toggle input {
        position: absolute;
        opacity: 0;
      }
      .toggle span {
        width: 34px;
        height: 19px;
        border-radius: 99px;
        background: var(--border);
        position: relative;
        transition: 0.18s;
      }
      .toggle span:after {
        content: '';
        position: absolute;
        width: 13px;
        height: 13px;
        top: 3px;
        left: 3px;
        background: #fff;
        border-radius: 50%;
        transition: 0.18s;
      }
      .toggle input:checked + span {
        background: var(--forest);
      }
      .toggle input:checked + span:after {
        transform: translateX(15px);
      }
      .toggle input:disabled + span {
        opacity: 0.48;
      }
      .toggle em {
        font-style: normal;
        font-size: 0.72rem;
        color: var(--muted);
      }
      .section-toggle em {
        font-weight: 700;
      }
    `,
  ],
})
export class AccessManagementComponent {
  readonly access = inject(PageAccessService);
  readonly roles: UserRole[] = ['agent', 'seniorAgent', 'manager', 'owner'];
  readonly labels = USER_ROLES;
  readonly sections = ACCESS_SECTIONS;
  allowed(key: string, role: UserRole) {
    return this.access.settings().permissions[key]?.[role] === true;
  }
  toggle(key: string, role: UserRole, allowed: boolean) {
    const current = this.access.settings();
    const permissions = {
      ...current.permissions,
      [key]: { ...current.permissions[key], [role]: allowed },
    };
    this.access.save({ permissions }).subscribe();
  }

  configurable(resource: AccessResourceDefinition, role: UserRole): boolean {
    return (
      roleCanOpen(resource, role) &&
      !(role === 'owner' && resource.key === 'administration.accessManagement')
    );
  }

  resourceState(resource: AccessResourceDefinition, role: UserRole): string {
    if (role === 'owner' && resource.key === 'administration.accessManagement') return 'Always';
    if (!roleCanOpen(resource, role)) return 'Restricted';
    return this.allowed(resource.key, role) ? 'Allowed' : 'Hidden';
  }

  sectionConfigurable(section: AccessSectionDefinition, role: UserRole): boolean {
    return section.resources.some((resource) => this.configurable(resource, role));
  }

  sectionAllowed(section: AccessSectionDefinition, role: UserRole): boolean {
    const resources = section.resources.filter((resource) => roleCanOpen(resource, role));
    return resources.length > 0 && resources.every((resource) => this.allowed(resource.key, role));
  }

  sectionPartiallyAllowed(section: AccessSectionDefinition, role: UserRole): boolean {
    const resources = section.resources.filter((resource) => roleCanOpen(resource, role));
    const allowed = resources.filter((resource) => this.allowed(resource.key, role)).length;
    return allowed > 0 && allowed < resources.length;
  }

  sectionState(section: AccessSectionDefinition, role: UserRole): string {
    if (!this.sectionConfigurable(section, role)) return 'Restricted';
    if (this.sectionPartiallyAllowed(section, role)) return 'Mixed';
    return this.sectionAllowed(section, role) ? 'All' : 'Hidden';
  }

  toggleSection(section: AccessSectionDefinition, role: UserRole, allowed: boolean) {
    if (!this.sectionConfigurable(section, role)) return;
    const current = this.access.settings();
    const permissions = { ...current.permissions };
    for (const resource of section.resources) {
      if (!this.configurable(resource, role)) continue;
      permissions[resource.key] = { ...permissions[resource.key], [role]: allowed };
    }
    this.access.save({ permissions }).subscribe();
  }
}
