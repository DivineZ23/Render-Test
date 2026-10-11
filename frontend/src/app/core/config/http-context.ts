import { HttpContextToken } from '@angular/common/http';

export const QUIET_HTTP_ERRORS = new HttpContextToken<boolean>(() => false);
