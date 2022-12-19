using Microsoft.AspNetCore.Authentication.Cookies;
using Stripe;
using Travelling.Middleware;
using Travelling.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = new PathString("/Account/Login");
});

GoogleMapsService googleMapsService = new GoogleMapsService();
Database database = new Database(googleMapsService);

builder.Services.AddSingleton(googleMapsService);
builder.Services.AddSingleton<HotelsService>();
builder.Services.AddSingleton<YandexMapsService>();
builder.Services.AddSingleton(database);

StripeConfiguration.ApiKey = "sk_test_51MCnEVCjbwlzSwz1ukS6kXYYPyewyl4FiC6qWUDlbnGqmN6LoZO0j07Ssg3GgFPs4s2SlcZxKHHLrBJWDSLhBVZj00mxuxtXrg";
builder.Logging.AddProvider(new LoggerProvider(database));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseMiddleware<RequestLoggingMiddleware>();

app.Run();