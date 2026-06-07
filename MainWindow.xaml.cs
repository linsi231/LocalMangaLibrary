using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace LocalMangaLibrary;

public partial class MainWindow : Window
{
    private const string AppHost = "local.manga.library";
    private readonly LocalApiService _api = new();
    private bool _cleanupStarted;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeWebViewAsync();
        Closing += HandleClosing;
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        if (_cleanupStarted)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "确认退出 Local Manga Library？\n退出时会清理当前应用目录下的 .cache 缓存。",
            "确认退出",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _cleanupStarted = true;
        try
        {
            if (Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.WebResourceRequested -= HandleWebResourceRequested;
            }

            Browser.Dispose();
        }
        catch
        {
            // The cache cleanup below is still safe if WebView2 has already torn down.
        }

        _api.ClearCacheOnShutdown();
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalMangaLibrary",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await Browser.EnsureCoreWebView2Async(environment);
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
        Browser.CoreWebView2.AddWebResourceRequestedFilter($"https://{AppHost}/*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += HandleWebResourceRequested;
        Browser.Source = new Uri($"https://{AppHost}/index.html");
    }

    private async void HandleWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.Response = await CreateResponseAsync(e.Request);
        }
        catch (Exception ex)
        {
            e.Response = JsonResponse(500, new { ok = false, error = ex.Message });
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task<CoreWebView2WebResourceResponse> CreateResponseAsync(CoreWebView2WebResourceRequest request)
    {
        var uri = new Uri(request.Uri);
        if (uri.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/thumbs/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/reader-cache/", StringComparison.OrdinalIgnoreCase))
        {
            return await _api.HandleAsync(Browser.CoreWebView2.Environment, request.Method, uri, request.Content);
        }

        var resourceName = uri.AbsolutePath.Trim('/').Replace('/', '.');
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            resourceName = "index.html";
        }

        var logicalName = $"LocalMangaLibrary.Resources.Web.{resourceName}";
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
        if (stream is null)
        {
            return TextResponse(404, "Not Found", "text/plain; charset=utf-8");
        }

        return Browser.CoreWebView2.Environment.CreateWebResourceResponse(
            stream,
            200,
            "OK",
            $"Content-Type: {MimeFor(resourceName)}\r\nCache-Control: no-cache");
    }

    private static string MimeFor(string name)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    private CoreWebView2WebResourceResponse TextResponse(int status, string text, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Browser.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(bytes),
            status,
            status == 200 ? "OK" : "Error",
            $"Content-Type: {contentType}");
    }

    private CoreWebView2WebResourceResponse JsonResponse(int status, object payload)
    {
        return _api.JsonResponse(Browser.CoreWebView2.Environment, status, payload);
    }
}
