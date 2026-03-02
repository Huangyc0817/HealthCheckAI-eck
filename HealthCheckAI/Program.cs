using Microsoft.EntityFrameworkCore;
using HealthCheckAI.Models;
using HealthCheckAI.Services;
using Xceed.Document.NET;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
// ✅ Session 需要 MemoryCache
builder.Services.AddDistributedMemoryCache();
// ✅ Session（只註冊一次）
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// DI
builder.Services.AddScoped<IAiPredictionService, AiModelService>();
// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// HttpClient
builder.Services.AddHttpClient<TranslationService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, EmailService>();

Xceed.Document.NET.Licenser.LicenseKey = "WDN52-Y4KUK-64RFP-2AFA";

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// ✅ Session 要放在 UseRouting 後、UseAuthorization 前（你這樣放是對的）
app.UseSession();
// 你目前沒有 UseAuthentication（因為你是用 Session 不是 Cookie Auth），所以只有授權也OK
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();