## Summary

## Security Notes

Optional reminders for security-relevant PRs:

- New API endpoints should use `RequirePermission` or `RequireAnyPermission`.
- Intentionally public endpoints should use `AllowAnonymous`, `EnableRateLimiting`, and `PublicEndpointJustification`.
- New DTO/input paths should include validation, preferably `AbstractValidator<T>`.
- File uploads should use a central validation helper such as `FileUploadValidation`.
- Frontend changes should avoid raw HTML sinks and keep access-token storage centralized.
- Security-sensitive changes should include tests or an explicit risk note.
