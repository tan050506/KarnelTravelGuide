using KarnelTravelGuide.Web.Data;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarnelTravelGuide.Web.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            // Lấy DbContext từ hệ thống
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Kiểm tra và đảm bảo DB đã sẵn sàng
            // context.Database.Migrate(); // Dùng dòng này nếu bạn có dùng Migration

            // 2. Định nghĩa tài khoản Admin mặc định
            var adminEmail = "admin@karneltravel.com";
            
            // Kiểm tra xem tài khoản này đã tồn tại chưa dựa trên Email
            var adminUser = await context.Accounts.FirstOrDefaultAsync(a => a.Email == adminEmail);

            if (adminUser == null)
            {
                var admin = new Account
                {
                    FullName = "Quản trị viên hệ thống",
                    Email = adminEmail,
                    Password = "Admin@123", // Lưu ý: Ở bản Identity nó tự Hash, còn ở đây bạn đang lưu text thuần
                    PhoneNumber = "0999000111",
                    Address = "Karnel Travel",
                    RoleId = 1,
                    AvatarUrl = "/images/avatars/default-admin.jpg"
                };

                // 3. Lưu vào Database
                context.Accounts.Add(admin);
                await context.SaveChangesAsync();
                
                Console.WriteLine("--> SeedData: Đã tạo tài khoản Admin mặc định thành công!");
            }
        }
    }
}