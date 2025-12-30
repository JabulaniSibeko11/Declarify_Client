using Declarify.Data;
using Declarify.Models;
using Declarify.Services;
using Declarify.Services.API;
using Declarify.Services.Methods;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout settings (FR 4.1.1 security)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;

        // Sign-in settings (FR 4.1.1)
        options.SignIn.RequireConfirmedAccount = false; // Changed to false since we're using email for login
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Login"; // Your custom login path
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.LogoutPath = "/Home/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();


// Application services (? MUST be before Build)
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IFormTaskService, FormTaskService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IVerificationService, VerificationService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IEmployeeDOIService, EmployeeDOIService>();
builder.Services.AddScoped<IReviewerService, ReviewerService>();
builder.Services.AddScoped<IReviewHelperService, ReviewHelperService>();

builder.Services.AddHttpClient<CentralHubApiService>();
builder.Services.AddScoped<CentralHubApiService>();

// MVC / Razor
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // ?? REQUIRED for Identity
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=LandingPage}/{id?}");

app.MapRazorPages();

app.Run();
