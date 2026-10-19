using System.Text.Encodings.Web;

namespace CulinaryBlog.Infrastructure.Services;

public static class WelcomeEmailTemplate
{
    public const string Subject = "Chào mừng bạn đến với Culinary Blog!";

    public static (string Html, string Text) Render(string fullName, string appUrl)
    {
        var name = HtmlEncoder.Default.Encode(fullName);
        var url = HtmlEncoder.Default.Encode(appUrl);
        return ($"""
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8"></head><body>
            <h1>Chào mừng đến với Culinary Blog!</h1>
            <p>Xin chào {name},</p>
            <p>Cảm ơn bạn đã tham gia cộng đồng yêu ẩm thực. Hãy khám phá và chia sẻ công thức yêu thích của bạn!</p>
            <p><a href="{url}" style="display:inline-block;padding:12px 20px;background:#b7410e;color:white;border-radius:6px">Khám phá Culinary Blog</a></p>
            <p>Chúc bạn có những trải nghiệm nấu ăn thú vị!</p>
            </body></html>
            """, $"Xin chào {fullName},\nChào mừng bạn đến với Culinary Blog!\nKhám phá và chia sẻ công thức yêu thích tại: {appUrl}\nChúc bạn có những trải nghiệm nấu ăn thú vị!");
    }
}
