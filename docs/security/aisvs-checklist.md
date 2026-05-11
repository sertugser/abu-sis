# Security Checklist

Based on OWASP AISVS

---

## Authentication

- [ ] Authentication required
- [ ] Password hashing enabled
- [ ] Session expiration configured

## Authorization

- [ ] RBAC implemented
- [ ] Unauthorized access blocked

## Input Validation

- [ ] SQL Injection prevention
- [ ] XSS prevention
- [ ] File upload validation

## API Security

- [ ] Rate limiting enabled
- [ ] CORS configured securely
- [ ] Sensitive endpoints protected

## Logging

- [ ] Security event logging
- [ ] Error logging enabled

## Dependency Security

- [ ] Dependency scanning enabled
- [ ] Vulnerable packages monitored

## Secret Management

- [ ] .env excluded from git
- [ ] API keys protected
