import { UserRole } from '../models/user.models';

export const USER_ROLES: Record<UserRole, string> = {
  agent: 'Agent',
  seniorAgent: 'Senior Agent',
  manager: 'Manager',
  owner: 'Owner',
  stateManager: 'State Manager',
};
