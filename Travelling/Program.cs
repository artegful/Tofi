using Microsoft.AspNetCore.Authentication.Cookies;
using Stripe;
using Travelling.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = new PathString("/Account/Login");
});

builder.Services.AddSingleton<GoogleMapsService>();
builder.Services.AddSingleton<HotelsService>();
builder.Services.AddSingleton<YandexMapsService>();
builder.Services.AddSingleton<Database>();
StripeConfiguration.ApiKey = "sk_test_51MCnEVCjbwlzSwz1ukS6kXYYPyewyl4FiC6qWUDlbnGqmN6LoZO0j07Ssg3GgFPs4s2SlcZxKHHLrBJWDSLhBVZj00mxuxtXrg";

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

app.Run();