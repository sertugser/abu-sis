using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Application.Auth.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Scheduling.Services;
using SIS.Application.StudentEligibility.Services;
using SIS.Application.AcademicLevel.Services;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.CourseOpening.Services;
using SIS.Application.TermClosing.Services;
using SIS.Application.FacultyPlanning.Services;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Interceptors;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor(); // AuditLog interceptor + IDOR kontrolleri için

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Audit SaveChanges interceptor — Scoped: her HTTP isteği kendi örneğini alır (thread-safe)
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// Add EF Core + PostgreSQL
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
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
builder.Services.AddScoped<ICourseRegistrationService, CourseRegistrationService>();
builder.Services.AddHostedService<DraftRegistrationCleanupService>(); // FR-44

// Register GPA Calculation service
builder.Services.AddScoped<IGpaCalculationService, GpaCalculationService>();
builder.Services.AddScoped<IEnrollmentForecastService, EnrollmentForecastService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IFacultyPlanningService, FacultyPlanningService>();
builder.Services.AddScoped<IAcademicOperationsService, AcademicOperationsService>();
builder.Services.AddScoped<IStudentEligibilityService, StudentEligibilityService>();
builder.Services.AddScoped<IAcademicLevelService, AcademicLevelService>();
builder.Services.AddScoped<IAcademicProgressionService, AcademicProgressionService>();
builder.Services.AddScoped<ICourseOpeningService, CourseOpeningService>();
builder.Services.AddScoped<ITermClosingService, TermClosingService>();

// Centralized config service — SystemConfigs tablosundan önbellekli okuma
builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();

// Admin services
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminStudentService, AdminStudentService>();
builder.Services.AddScoped<IAdminInstructorService, AdminInstructorService>();
builder.Services.AddScoped<IAdminTermService, AdminTermService>();
builder.Services.AddScoped<IAcademicCalendarService, AcademicCalendarService>();
builder.Services.AddScoped<IAdminAcademicStructureService, AdminAcademicStructureService>();
builder.Services.AddScoped<IAdminCourseCatalogService, AdminCourseCatalogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await SeedData.SeedAsync(context, userManager, roleManager);
    await SeedData.AssignInstructorCoursesAsync(context);
}

app.Run();
