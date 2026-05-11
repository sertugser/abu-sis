using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SIS.API.Security;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Admin.Services;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.Identity.Services;
using SIS.Application.Auth.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.ElectiveGroups.Services;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.UserPreferences.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Recommendation.Services;
using SIS.Application.Scheduling.Services;
using SIS.Application.StudentEligibility.Services;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.AcademicLevel.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.CourseOpening.Services;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.CourseEvaluation.Services;
using SIS.Application.Announcement.Services;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;
using SIS.Infrastructure.Services.CourseEvaluation;
using SIS.Infrastructure.Services.Announcement;
using SIS.Infrastructure.Services.CalendarGenerator;

var builder = WebApplication.CreateBuilder(args);
StartupSecurityValidator.Validate(builder.Configuration, builder.Environment);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddScoped<SIS.API.Filters.SensitiveDataAccessFilter>();

// Rate limiting for audit log endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthLogin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 8,
                QueueLimit = 0,
            }));
    options.AddPolicy("PublicRead", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60,
                QueueLimit = 0,
            }));
    options.AddPolicy("PublicAttendanceQr", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20,
                QueueLimit = 0,
            }));
    options.AddPolicy("AuditLogRead", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 30,
                QueueLimit = 0,
            }));
    options.AddPolicy("AuditLogExport", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 3,
                QueueLimit = 0,
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add EF Core + PostgreSQL
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
});

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5; // FR-07
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = StartupSecurityValidator.GetRequiredJwtKey(builder.Configuration);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // SignalR WebSocket bağlantısı header gönderemez — token query param'dan gelir.
    // Bu event sadece /hubs/* path'lerinde devreye girer; HTTP API çağrıları
    // standart Authorization header'ını kullanmaya devam eder.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// SignalR — auto-schedule progress, real-time bildirimler vb. için.
builder.Services.AddSignalR();
builder.Services.AddSingleton<SIS.Application.Scheduling.Services.IScheduleProgressReporter,
    SIS.API.Hubs.SignalRScheduleProgressReporter>();
// Cancel registry: aktif auto-schedule run'larının CTS'ini kullanıcı bazında tutar.
// Singleton — tüm hub instance'ları aynı registry üzerinden çalışır.
builder.Services.AddSingleton<SIS.Application.Scheduling.Services.IScheduleCancelRegistry,
    SIS.Infrastructure.Services.Scheduling.ScheduleCancelRegistry>();

// Register Auth services
builder.Services.AddScoped<IAuthService, AuthService>();

// Elective Groups
builder.Services.AddScoped<IElectiveGroupScopeValidator, ElectiveGroupScopeValidator>();

// Register Course Registration services
builder.Services.AddScoped<IRegistrationValidationService, RegistrationValidationService>();
builder.Services.AddScoped<IAdvisorConstraintService, AdvisorConstraintService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<ICourseRegistrationService, CourseRegistrationService>();
builder.Services.AddHostedService<DraftRegistrationCleanupService>(); // FR-44
builder.Services.AddHostedService<RegistrationCleanupJob>();
builder.Services.AddHostedService<EvaluationPeriodOpenerJob>();
builder.Services.AddHostedService<AuditRetentionService>();

// Register GPA Calculation service
builder.Services.AddScoped<IGpaCalculationService, GpaCalculationService>();
builder.Services.AddScoped<EnrollmentForecastService>();
builder.Services.AddScoped<IEnrollmentForecastService>(serviceProvider => serviceProvider.GetRequiredService<EnrollmentForecastService>());
builder.Services.AddScoped<IEnrollmentForecastCacheInvalidator>(serviceProvider => serviceProvider.GetRequiredService<EnrollmentForecastService>());
builder.Services.AddSchedulingConstraints();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ISchedulingService>(sp => sp.GetRequiredService<SchedulingService>());
builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();
builder.Services.AddScoped<ClassroomSuggestionService>();
builder.Services.AddScoped<IFacultyPlanningService, FacultyPlanningService>();
builder.Services.AddScoped<IAcademicOperationsService, AcademicOperationsService>();
builder.Services.AddScoped<IStudentEligibilityService, StudentEligibilityService>();
builder.Services.AddScoped<ICourseDifficultyService, CourseDifficultyService>();
builder.Services.AddScoped<ICriticalPathService, CriticalPathService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IAcademicLevelService, AcademicLevelService>();
builder.Services.AddScoped<IAcademicProgressionService, AcademicProgressionService>();
// Faz 3.C: Eski monolitik CourseOpeningService 4 ayrı servise + 1 helper'a bölündü
builder.Services.AddScoped<SIS.Infrastructure.Services.CourseOpening.CourseOpeningHelpers>();
builder.Services.AddScoped<ICourseSectionLifecycleService, SIS.Infrastructure.Services.CourseOpening.CourseSectionLifecycleService>();
builder.Services.AddScoped<ICourseOpeningQueryService, SIS.Infrastructure.Services.CourseOpening.CourseOpeningQueryService>();
builder.Services.AddScoped<IInstructorAssignmentService, SIS.Infrastructure.Services.CourseOpening.InstructorAssignmentService>();
builder.Services.AddScoped<ITermCourseTransferService, SIS.Infrastructure.Services.CourseOpening.TermCourseTransferService>();
builder.Services.AddScoped<IExcelScheduleService, ExcelScheduleService>();

// Admin services
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminStudentService, AdminStudentService>();
builder.Services.AddScoped<IAdminInstructorService, AdminInstructorService>();
builder.Services.AddScoped<IAdminTermService, AdminTermService>();
builder.Services.AddScoped<SIS.Application.AcademicCalendar.Services.IAcademicCalendarService, AcademicCalendarService>();
builder.Services.AddScoped<ICalendarTemplateService, CalendarTemplateService>();
builder.Services.AddScoped<IHolidayCalculatorService, HolidayCalculatorService>();
builder.Services.AddScoped<IAcademicCalendarRuleService, AcademicCalendarRuleService>();
builder.Services.AddScoped<ICalendarGeneratorService, CalendarGeneratorService>();
builder.Services.AddScoped<IAdminAcademicStructureService, AdminAcademicStructureService>();
builder.Services.AddScoped<IOsymRegistrationCalendarService, OsymRegistrationCalendarService>();
builder.Services.AddScoped<IProgramQuotaService, ProgramQuotaService>();
builder.Services.AddScoped<IAdminCourseCatalogService, AdminCourseCatalogService>();
builder.Services.AddScoped<ISystemConfigProvider, SystemConfigProvider>();
builder.Services.AddScoped<IWebModuleSettingsProvider, WebModuleSettingsProvider>();

// RBAC (Phase 1)
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IMenuService, MenuService>();

// Course Evaluation + Internal Announcements
builder.Services.AddScoped<ICourseEvaluationService, CourseEvaluationService>();
builder.Services.AddScoped<IInternalAnnouncementService, InternalAnnouncementService>();

// Time Provider
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = "An unexpected error occurred.",
                status = StatusCodes.Status500InternalServerError,
                traceId = context.TraceIdentifier,
            });
        });
    });
    app.UseHsts();
}

app.UseMiddleware<SIS.API.Middleware.ErrorLoggingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<SIS.API.Hubs.ScheduleProgressHub>("/hubs/schedule-progress");

// Seed database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await SeedData.SeedAsync(context, userManager, roleManager);

    // Auto-schedule algoritma test verisi — Mühendislik fakültesinin gerçek dönem verisi
    // (Güz 2024-25 + Bahar 2025-26, 162 section, hoca atanmamış). Idempotent — varsa skip.
    // Memory: project_autoschedule_test_data.md, project_autoschedule_test_disabled_constraints.md
    await SeedData.SeedAutoScheduleTestDataAsync(context);
}

app.Run();
