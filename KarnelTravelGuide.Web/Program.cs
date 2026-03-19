using KarnelTravelGuide.Web.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ĐĂNG KÝ DỊCH VỤ SESSION (BẮT BUỘC)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add services to the container
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 3. CHẠY SEED DATA
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Lỗi khi Seed Data: " + ex.Message);
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Tạm tắt nếu bạn chạy http://localhost:5082
app.UseStaticFiles(); // Đảm bảo load được ảnh đại diện/CSS

app.UseRouting();

// 4. KÍCH HOẠT MIDDLEWARE SESSION (PHẢI ĐẶT Ở ĐÂY)
app.UseSession(); 

app.UseAuthorization();

app.MapStaticAssets();

// 5. Cấu hình Route
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();