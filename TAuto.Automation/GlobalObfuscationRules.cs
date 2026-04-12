using System.Reflection;

// HƯỚNG DẪN DÀNH CHO OBFUSCATOR BẤT KỲ (ConfuserEx, Dotfuscator, Obfuscar...):
// Lệnh dưới đây yêu cầu trình mã hóa KHÔNG ĐƯỢC PHÉP ĐỔI TÊN (renaming) các Class/Method/Interface
// đang ở trạng thái Public bên trong thư viện TAuto.Automation (bao gồm BotBase).
// Điều này sống còn để C# Bot (.dll) của người dùng có thể nạp thành công vào Worker Obfuscated.

[assembly: Obfuscation(Feature = "renaming", Exclude = true, ApplyToMembers = true)]
