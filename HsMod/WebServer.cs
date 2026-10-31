using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static HsMod.PluginConfig;

namespace HsMod
{
    public static class WebServer
    {
        public static HttpListener httpListener = new HttpListener
        {
            AuthenticationSchemes = AuthenticationSchemes.Anonymous
        };

        public static Task listenerTask;
        public static string shellCommand;
        public static bool shellCommandLock;
        public static bool pluginConfigLock;
        public static bool updateLock;

        public static void Restart()
        {
            try
            {
                httpListener.Stop();
                listenerTask = null;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }
            finally
            {
                try
                {
                    httpListener.Prefixes.Remove($"http://+:{CommandConfig.webServerPort}/");
                    Start();
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                }
            }
        }


        public static void Start()
        {
            httpListener.Prefixes.Add($"http://+:{CommandConfig.webServerPort}/");
            httpListener.Start();
            listenerTask = Task.Run(ListenAsync);
        }

        private static async Task ListenAsync()
        {
            while (httpListener.IsListening)
            {
                var context = await httpListener.GetContextAsync(); // Fully asynchronous, non-blocking
                _ = Task.Run(() => ProcessRequestAsync(context));   // Start processing each request in a separate task
            }
        }

        // 分解相关接口只接受本机发来、Host/Origin 都是本机地址、且带自定义请求头的请求：
        // 自定义头会触发 CORS 预检（这里不回 CORS 头，所以其他网页提交不了），Host 检查挡住 DNS rebinding
        private static bool IsTrustedDisenchantRequest(HttpListenerRequest request)
        {
            string origin = request.Headers["Origin"];
            bool localHost = request.Url.Host == "127.0.0.1" || request.Url.Host == "localhost";
            bool localOrigin = string.IsNullOrEmpty(origin)
                || origin == $"http://127.0.0.1:{request.Url.Port}" || origin == $"http://localhost:{request.Url.Port}";
            return IPAddress.IsLoopback(request.RemoteEndPoint.Address) && localHost && localOrigin
                && request.Headers["X-HsMod-Disenchant"] == "confirm";
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            context.Response.StatusCode = 200;
            string rawUrLower = request.RawUrl.ToLower();

            Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"{request.RemoteEndPoint.ToString()} => {request.RawUrl}");
            Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"{DateTime.Now.ToString("yyyy/MM/dd_HH:mm:ss")} {request.Url}");

            if (rawUrLower == "/webshell" && request.HttpMethod == "POST" && !shellCommandLock)
            {
                shellCommandLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "command" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("command", out string command))
                        {
                            // Execute the command asynchronously
                            output = await WebApi.RunShellCommandAsync(command);
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'command' field is required.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error executing command: {ex.Message}";
                }
                finally
                {
                    shellCommandLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(output);
                    }
                }
            }
            else if (rawUrLower == "/disenchant/settings" && request.HttpMethod == "POST")
            {
                // 保存 /disenchant 页面自己的设置（最少对局数、范围），校验和分解请求相同
                string output;
                if (!IsTrustedDisenchantRequest(request))
                {
                    context.Response.StatusCode = 403;
                    output = "forbidden";
                }
                else
                {
                    try
                    {
                        string requestBody;
                        using (var reader = new StreamReader(request.InputStream))
                            requestBody = await reader.ReadToEndAsync();
                        var values = Newtonsoft.Json.JsonConvert.DeserializeObject<Utils.DisenchantSettings.Values>(requestBody);
                        if (values == null) throw new Exception("no settings");
                        Utils.DisenchantSettings.Save(values);
                        output = "ok";
                    }
                    catch (Exception ex)
                    {
                        context.Response.StatusCode = 400;
                        output = ex.Message;
                    }
                }
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteLineAsync(output);
                }
            }
            else if (rawUrLower == "/disenchant" && request.HttpMethod == "POST")
            {
                string output;
                if (!IsTrustedDisenchantRequest(request))
                {
                    context.Response.StatusCode = 403;
                    output = "forbidden";
                }
                else
                {
                    string requestBody;
                    using (var reader = new StreamReader(request.InputStream))
                        requestBody = await reader.ReadToEndAsync();
                    // 页面提交的是它显示并经用户确认的那份清单：[{ "id": 卡牌ID, "premium": "NORMAL"/"GOLDEN", "count": 张数 }, ...]
                    var cards = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Utils.Disenchanter.RequestedCard>>(requestBody);
                    if (cards == null || cards.Count == 0)
                    {
                        context.Response.StatusCode = 400;
                        output = "no cards";
                    }
                    else
                    {
                        context.Response.StatusCode = Utils.Disenchanter.Request(cards, out output) ? 200 : 409;
                    }
                }
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteLineAsync(output);
                }
            }
            else if (rawUrLower == "/config" && request.HttpMethod == "POST" && !pluginConfigLock)
            {
                pluginConfigLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "key" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("key", out string key) && json.TryGetValue("value", out string value))
                        {
                            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                            {

                                context.Response.StatusCode = 400; // Bad Request
                                output = "Invalid request: key or value not found.";
                            }
                            else
                            {
                                string realConfigKey = key.EndsWith(".name") ? key : LocalizationManager.GetLangKey(key);
                                if (!realConfigKey.EndsWith(".name"))
                                {
                                    context.Response.StatusCode = 400; // Bad Request
                                    output = "Invalid request: key not found.";
                                }
                                else
                                {
                                    context.Response.StatusCode = WebApi.RunPluginConfigAsync(realConfigKey, value, out string newValue);
                                    output = newValue;
                                }
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'key' and 'value' field is required.";
                        }

                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error config: {ex.Message}";
                }
                finally
                {
                    pluginConfigLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"status\":{context.Response.StatusCode},\"output\":\"{output}\"}}");
                    }
                }
            }
            else if (rawUrLower == "/update" && request.HttpMethod == "POST" && !updateLock)
            {
                updateLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "key" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("key", out string key) && json.TryGetValue("value", out string value))
                        {
                            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                            {

                                context.Response.StatusCode = 400; // Bad Request
                                output = "Invalid request: key or value not found.";
                            }
                            else
                            {
                                if (key.ToLower().Equals("hsskins.cfg") || key.ToLower().Equals("hsskins"))
                                {
                                    context.Response.StatusCode = WebApi.UpdateHsSkinsCfg(value, out string newValue);
                                    output = newValue;
                                }
                                else
                                {
                                    context.Response.StatusCode = 400; // Bad Request
                                    output = "Invalid request: 'key' not support.";
                                }
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'key' and 'value' field is required.";
                        }

                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error update: {ex.Message}";
                }
                finally
                {
                    updateLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"status\":{context.Response.StatusCode},\"output\":\"{output}\"}}");
                    }
                }
            }
            else if (rawUrLower.StartsWith("/api/config") && request.HttpMethod == "GET")
            {
                // Return config metadata as JSON
                context.Response.ContentType = "application/json; charset=UTF-8";
                string output = string.Empty;
                try
                {
                    // Parse lang parameter from query string
                    string lang = null;
                    var queryString = request.QueryString;
                    if (queryString["lang"] != null && !string.IsNullOrEmpty(queryString["lang"]))
                    {
                        lang = queryString["lang"];
                    }
                    output = WebApi.GetAllConfigMetadata(lang);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    output = $"{{\"error\":\"{ex.Message}\"}}";
                }
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteLineAsync(output);
                }
            }
            else
            {
                context.Response.ContentType = DetermineContentType(rawUrLower);

                string preUrl = DetermineFilePath(rawUrLower);
                if (VaildFilePath(preUrl))   // 优先查找本地文件
                {
                    context.Response.ContentType = GetMimeType(Path.GetExtension(preUrl));
                    var file = await File.ReadAllBytesAsync(preUrl);
                    await context.Response.OutputStream.WriteAsync(file, 0, file.Length);
                }
                else if (rawUrLower == "/safeimg")
                {
                    //var safeimg = Convert.FromBase64String(WebPage.SafeImg);
                    //context.Response.OutputStream.Write(safeimg, 0, safeimg.Length);
                }
                else
                {
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(Route(rawUrLower).ToString());
                    }
                }
            }
            context.Response.OutputStream.Close();
        }


        private static string DetermineContentType(string rawUrl)
        {
            if (rawUrl.EndsWith(".js"))
                return "text/javascript; charset=UTF-8";
            if (rawUrl.EndsWith(".jpg") || rawUrl.EndsWith(".jpeg") || rawUrl == "/safeimg")
                return "image/jpeg";
            if (rawUrl.EndsWith(".txt") || rawUrl.EndsWith(".log") || rawUrl.EndsWith(".cfg"))
                return "text/plain; charset=UTF-8";
            return "text/html; charset=UTF-8";
        }

        private static string DetermineFilePath(string rawUrl)
        {
            string preUrl = rawUrl.Substring(1);
            switch (preUrl)
            {
                case "hslog": preUrl = hsLogPath.Value; break;
                // case "beplog": preUrl = "BepInEx/LogOutput.log"; break;
                default: preUrl = Path.Combine(PluginConfig.HsModWebSite, rawUrl.Substring(1)); break;
            }
            return preUrl;
        }
        private static bool VaildFilePath(string rawUrl)
        {
            if (File.Exists(rawUrl))
            {
                string rawUrlFilePath = Path.GetFullPath(rawUrl);
                string websitePath = Path.GetFullPath(PluginConfig.HsModWebSite);
                //string gameRootPath = Path.GetFullPath(BepInEx.Paths.GameRootPath);
                //string bepinexRootPath = Path.GetFullPath(BepInEx.Paths.BepInExRootPath);
                if (rawUrlFilePath.StartsWith(websitePath))
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        public static StringBuilder Route(string url = "")
        {
            switch (url)
            {
                case "/info":
                    return WebPage.InfoPage();
                case "/collection":
                    return WebPage.CollectionPage();
                case "/disenchant":
                    return WebPage.DisenchantPage();
                case "/pack":
                    return WebPage.PackPage();
                case "/skins":
                    return WebPage.SkinsPage();
                case "/lettuce":
                    return WebPage.MercenariesLettucePage();
                case "/mercenaries":
                    return WebPage.MercenariesPage();
                case "/matchlog":
                    return WebPage.MatchLogPage();
                case "/alive":
                    return WebPage.AlivePage();
                case "/bepinex.min.log":
                    return WebPage.BepInExLogPage(666);
                case "/bepinex.log":
                    return WebPage.BepInExLogPage();
                case "/hsmod.cfg":
                    return WebPage.HsModCfgPage("HsMod.cfg");
                case "/hsskins.cfg":
                    return WebPage.HsModCfgPage("HsSkins.cfg");
                case "/config":
                    return WebPage.ConfigPage();
                case "":
                case "/":
                case "/home":
                    return WebPage.HomePage();
                case "/about":
                    return WebPage.AboutPage();
                case "/shell":
                    return WebPage.ShellPage();
                case "/jquery.min.js":
                    return new StringBuilder(FileManager.ReadEmbeddedFile("./WebResources/jquery.min.js"));
                default:
                    return new StringBuilder();
            }
        }

        static string GetMimeType(string extension)
        {
            var mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".cfg", "text/plain; charset=UTF-8" },
                { ".txt", "text/plain; charset=UTF-8" },
                { ".log", "text/plain; charset=UTF-8" },
                { ".html", "text/html; charset=UTF-8" },
                { ".htm", "text/html; charset=UTF-8" },
                { ".css", "text/css; charset=UTF-8" },
                { ".js", "application/javascript; charset=UTF-8" },
                { ".json", "application/json; charset=UTF-8" },
                { ".xml", "application/xml; charset=UTF-8" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" },
                { ".svg", "image/svg+xml" },
                { ".bmp", "image/bmp" },
                { ".mp3", "audio/mpeg" },
                { ".wav", "audio/wav" },
                { ".mp4", "video/mp4" },
                { ".pdf", "application/pdf" },
                { ".zip", "application/zip" },
            };

            return mimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : "application/octet-stream";
        }

    }
}
