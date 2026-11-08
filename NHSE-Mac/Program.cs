using NHSE.Core;
using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:8765");
var app = builder.Build();
var root = Path.GetFullPath(AppContext.BaseDirectory);
var outputRoot = Path.Combine(root, "edited-saves");
var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support/MacNativeGames/Animal Crossing/profile/save");
app.MapEditorApi(outputRoot);

app.MapGet("/", () => Results.Content(File.ReadAllText(Path.Combine(root, "index.html")), "text/html; charset=utf-8"));
app.MapGet("/api/default", () => {
    var paths = Directory.Exists(defaultRoot) ? Directory.GetDirectories(defaultRoot).Where(p => File.Exists(Path.Combine(p, "main.dat"))).ToArray() : [];
    return Results.Json(paths);
});
app.MapGet("/api/pick-folder", async () => {
    try
    {
        var start = new System.Diagnostics.ProcessStartInfo("/usr/bin/osascript")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add("POSIX path of (choose folder with prompt \"选择动森存档文件夹\")");
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("无法打开 macOS 文件夹选择窗口。");
        var path = (await process.StandardOutput.ReadToEndAsync()).Trim();
        var error = (await process.StandardError.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            if (error.Contains("-128", StringComparison.Ordinal)) return Results.Json(new { cancelled = true });
            return Results.BadRequest(new { error = string.IsNullOrEmpty(error) ? "文件夹选择失败。" : error });
        }
        return Results.Json(new { cancelled = false, path = path.TrimEnd('/') });
    }
    catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/reveal", async (RevealRequest request) => {
    try
    {
        var outputFullPath = Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(request.Path);
        if (!target.StartsWith(outputFullPath, StringComparison.Ordinal) || !Directory.Exists(target))
            return Results.BadRequest(new { error = "只能在 Finder 中显示本工具刚生成的存档文件夹。" });
        var start = new System.Diagnostics.ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        start.ArgumentList.Add("-R");
        start.ArgumentList.Add(target);
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("无法打开 Finder。");
        await process.WaitForExitAsync();
        return process.ExitCode == 0 ? Results.Ok() : Results.BadRequest(new { error = "Finder 打开失败。" });
    }
    catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/inspect", (InspectRequest request) => {
    try {
        var path = ValidatePath(request.Path);
        var save = HorizonSave.FromFolder(path);
        if (!save.ValidateSizes()) return Results.BadRequest(new { error = "存档版本或文件大小不受当前 NHSE 支持。" });
        var invalid = save.GetInvalidHashes().Count();
        return Results.Json(new {
            path, island = save.Players[0].Personal.TownName,
            players = save.Players.Select((p, i) => new { index = i, name = p.Personal.PlayerName, wallet = p.Personal.Wallet.Value, bank = p.Personal.Bank.Value, miles = p.Personal.NookMiles.Value }).ToArray(),
            invalidHashes = invalid
        });
    } catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/save", (EditRequest request) => {
    string? destination = null;
    try {
        var source = ValidatePath(request.Path);
        var original = HorizonSave.FromFolder(source);
        if (!original.ValidateSizes() || original.GetInvalidHashes().Any()) return Results.BadRequest(new { error = "源存档校验未通过，已停止保存。" });
        if (request.Player < 0 || request.Player >= original.Players.Count) return Results.BadRequest(new { error = "玩家编号无效。" });
        if (request.Wallet > 999_999_999 || request.Bank > 999_999_999 || request.Miles > 999_999_999) return Results.BadRequest(new { error = "数值不能超过 999,999,999。" });
        Directory.CreateDirectory(outputRoot);
        destination = Path.Combine(outputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        CopyDirectory(source, destination);
        var save = HorizonSave.FromFolder(destination);
        var personal = save.Players[request.Player].Personal;
        var wallet = personal.Wallet; wallet.Value = request.Wallet; personal.Wallet = wallet;
        var bank = personal.Bank; bank.Value = request.Bank; personal.Bank = bank;
        var miles = personal.NookMiles; miles.Value = request.Miles; personal.NookMiles = miles;
        save.Save((uint)RandomNumberGenerator.GetInt32(int.MaxValue));
        var check = HorizonSave.FromFolder(destination);
        if (!check.ValidateSizes() || check.GetInvalidHashes().Any() ||
            check.Players[request.Player].Personal.Wallet.Value != request.Wallet ||
            check.Players[request.Player].Personal.Bank.Value != request.Bank ||
            check.Players[request.Player].Personal.NookMiles.Value != request.Miles)
            throw new InvalidDataException("保存后的存档校验失败。");
        return Results.Json(new { destination });
    } catch (Exception e) {
        if (destination != null && Directory.Exists(destination)) Directory.Delete(destination, true);
        return Results.BadRequest(new { error = e.Message });
    }
});

app.Run();

static string ValidatePath(string? value) {
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("请输入存档文件夹路径。");
    var input = value.Trim();
    // Paths copied from terminals or documentation may be wrapped in quotes.
    if (input.Length >= 2 && ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\'')))
        input = input[1..^1].Trim();
    var path = Path.GetFullPath(input);
    if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "main.dat")) || !File.Exists(Path.Combine(path, "mainHeader.dat")))
        throw new DirectoryNotFoundException("请选择包含 main.dat 和 mainHeader.dat 的存档文件夹。");
    return path;
}
static void CopyDirectory(string source, string destination) {
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
}
record InspectRequest(string Path);
record EditRequest(string Path, int Player, uint Wallet, uint Bank, uint Miles);
record RevealRequest(string Path);
