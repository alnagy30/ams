using System.Globalization;
using Bcrypt = BCrypt.Net.BCrypt;
using Microsoft.Data.Sqlite;

namespace BH_AEP.ResetAdmin;

internal static class Program
{
    private static int Main(string[] args)
    {
        var db = GetArg(args, "--db") ?? FindDefaultDb();
        var username = GetArg(args, "--username") ?? "admin";
        var password = GetArg(args, "--password");
        var nonInteractive = args.Contains("--force");

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (string.IsNullOrWhiteSpace(db) || !File.Exists(db))
        {
            Console.Error.WriteLine("لم يتم العثور على ملف قاعدة البيانات.");
            Console.Error.WriteLine("الاستخدام:");
            Console.Error.WriteLine("  ResetAdminPassword.exe --db <path-to-bh-aep.db> --username admin --password <جديد>");
            return 2;
        }

        if (password is null)
        {
            if (nonInteractive)
            {
                Console.Error.WriteLine("--force يتطلب تمرير --password.");
                return 2;
            }

            Console.Write("أدخل كلمة المرور الجديدة: ");
            password = ReadMasked();
            if (string.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine("كلمة المرور لا يمكن أن تكون فارغة.");
                return 2;
            }
        }

        if (password.Length < 8)
        {
            Console.Error.WriteLine("كلمة المرور يجب ألا تقل عن 8 أحرف.");
            return 2;
        }

        try
        {
            var hash = Bcrypt.HashPassword(password, Bcrypt.GenerateSalt(12));
            var connString = new SqliteConnectionStringBuilder
            {
                DataSource = db,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            using var conn = new SqliteConnection(connString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout = 10000;";
                cmd.ExecuteNonQuery();
            }

            int userId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id FROM Users WHERE Username = $u AND IsDeleted = 0 LIMIT 1;";
                cmd.Parameters.AddWithValue("$u", username);
                userId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            if (userId <= 0)
            {
                Console.Error.WriteLine($"لا يوجد مستخدم باسم «{username}».");
                return 1;
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE Users
                    SET PasswordHash = $h,
                        ForcePasswordChange = 1,
                        IsLocked = 0,
                        FailedLoginAttempts = 0,
                        LockedUntil = NULL,
                        CurrentSessionToken = NULL
                    WHERE Id = $id;
                    """;
                cmd.Parameters.AddWithValue("$h", hash);
                cmd.Parameters.AddWithValue("$id", userId);
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine($"تمت إعادة تعيين كلمة مرور المستخدم «{username}» بنجاح.");
            Console.WriteLine("سيُطلب منه تغيير كلمة المرور عند أول تسجيل دخول، وستنتهي جميع الجلسات الحالية.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("خطأ أثناء التنفيذ: " + ex.Message);
            return 1;
        }
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static string? FindDefaultDb()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 4; i++)
        {
            var candidate = Path.Combine(current, "data", "bh-aep.db");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null) break;
            current = parent;
        }
        return null;
    }

    private static string ReadMasked()
    {
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && buffer.Length > 0)
            {
                buffer.Length -= 1;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return buffer.ToString();
    }
}
