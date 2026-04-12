using System.Reflection;

// HƯỚNG DẪN DÀNH CHO OBFUSCATOR BẤT KỲ (ConfuserEx, Dotfuscator, Obfuscar...):
// Lệnh dưới đây yêu cầu trình mã hóa KHÔNG ĐƯỢC PHÉP ĐỔI TÊN (renaming) các Class/Method/Interface
// đang ở trạng thái Public bên trong thư viện TAuto.Core.
// Tính năng "Control Flow" (mã hóa luồng) hoặc "String Encryption" vẫn hiển nhiên được áp dụng.

[assembly: Obfuscation(Feature = "renaming", Exclude = true, ApplyToMembers = true)]
