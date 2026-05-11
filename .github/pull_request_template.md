## Summary

## ASVS Security Checklist

- [ ] Every new API endpoint has `RequirePermission` or `RequireAnyPermission`.
- [ ] Every intentionally public endpoint has `AllowAnonymous`, `EnableRateLimiting`, and `PublicEndpointJustification`.
- [ ] Every new DTO/input path has validation, preferably `AbstractValidator<T>`.
- [ ] Every file upload uses a central validation helper such as `FileUploadValidation`.
- [ ] Frontend changes avoid raw HTML sinks and keep access-token storage centralized.
- [ ] Security-sensitive changes include tests or an explicit risk note.
