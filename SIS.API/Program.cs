using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Application.Auth.Services;
using SIS.Application.CourseRegistration.Services;
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
using SIS.Application.TermClosing.Services;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.CourseEvaluation.Services;
using SIS.Application.Announcement.Services;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.CourseEvaluation;
using SIS.Infrastructure.Services.Announcement;
using SIS.Infrastructure.Services.CalendarGenerator;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
    };
});

// Register Auth services
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Course Registration services
builder.Services.AddScoped<IRegistrationValidationService, RegistrationValidationService>();
builder.Services.AddScoped<IAdvisorConstraintService, AdvisorConstraintService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<ICourseRegistrationService, CourseRegistrationService>();
builder.Services.AddHostedService<DraftRegistrationCleanupService>(); // FR-44
builder.Services.AddHostedService<TermTransitionBackgroundService>(); // Otomatik dönem geçişleri
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
builder.Services.AddScoped<ICourseOpeningService, CourseOpeningService>();
builder.Services.AddScoped<ITermClosingService, TermClosingService>();
builder.Services.AddScoped<IExcelScheduleService, ExcelScheduleService>();

// Admin services
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminStudentService, AdminStudentService>();
builder.Services.AddScoped<IAdminInstructorService, AdminInstructorService>();
builder.Services.AddScoped<IAdminTermService, AdminTermService>();
builder.Services.AddScoped<IAcademicCalendarService, AcademicCalendarService>();
builder.Services.AddScoped<ICalendarTemplateService, CalendarTemplateService>();
builder.Services.AddScoped<IHolidayCalculatorService, HolidayCalculatorService>();
builder.Services.AddScoped<IAcademicCalendarRuleService, AcademicCalendarRuleService>();
builder.Services.AddScoped<ICalendarGeneratorService, CalendarGeneratorService>();
builder.Services.AddScoped<IAdminAcademicStructureService, AdminAcademicStructureService>();
builder.Services.AddScoped<IAdminCourseCatalogService, AdminCourseCatalogService>();
builder.Services.AddScoped<ISystemConfigProvider, SystemConfigProvider>();

// Course Evaluation + Internal Announcements
builder.Services.AddScoped<ICourseEvaluationService, CourseEvaluationService>();
builder.Services.AddScoped<IInternalAnnouncementService, InternalAnnouncementService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<SIS.API.Middleware.ErrorLoggingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

// Seed database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await SeedData.SeedAsync(context, userManager, roleManager);
}

app.Run();
