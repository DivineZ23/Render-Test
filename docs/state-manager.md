# State Manager

State Manager is a separate view-only role, not a management promotion. Existing role values and permissions are unchanged.

1. As Owner, open User Management and use the user's Role selector to assign State Manager. Supply a reason for the audit log. Approval remains a separate step for pending accounts.
2. Open Access Management and enable the desired pages in the State Manager column. All pages start disabled. Property actions and Create Listing cannot be enabled.
3. The State Manager can read granted pages, use search and filters, and copy displayed information. They cannot change business data, notices, payouts, roles, access settings, profiles, or uploads. Sign-out remains available.

Private API requests check the current database role and current page grants. Older login tokens do not preserve write privileges after a role change. Unknown private endpoints fail closed. Public website data and session/navigation bootstrap endpoints remain available independently of dashboard page grants.

Management read endpoints (for example, agent analytics and audit logs) are permitted only after the page check succeeds. State Managers remain excluded from the agent commission roster. Role changes are Owner-only and audited; Owner accounts and self-role changes are protected.
