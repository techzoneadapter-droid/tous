using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("AKMasterSocical Updater")]
[assembly: AssemblyProduct("AKMasterSocical Clean Edition")]
[assembly: AssemblyVersion("1.0.0.19")]
[assembly: AssemblyFileVersion("1.0.0.19")]

namespace AKMasterSocicalClean
{
    internal static class UpdaterProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string appPath = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AKMasterSocical.exe");
            Application.Run(new UpdaterForm(appPath));
        }
    }

    internal sealed class ReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal sealed class ReleaseInfo
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public bool prerelease { get; set; }
        public List<ReleaseAsset> assets { get; set; }
    }

    internal sealed class UpdaterForm : Form
    {
        private const string ApiUrl = "https://api.github.com/repos/techzoneadapter-droid/tous/releases/latest";
        private readonly string appPath;
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Button action = new Button();
        private ReleaseInfo release;
        private Version currentVersion;
        private bool updateAvailable;
        private bool checkFailed;

        public UpdaterForm(string appPath)
        {
            this.appPath = Path.GetFullPath(appPath);
            Text = "AKMasterSocical Update";
            Width = 520;
            Height = 250;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            BuildUi();
            Shown += async delegate { await CheckAsync(); };
        }

        private void BuildUi()
        {
            var title = new Label { Text = "Cập nhật AKMasterSocical", Left = 24, Top = 22, AutoSize = true, Font = new Font("Segoe UI Semibold", 16F), ForeColor = Color.FromArgb(23, 43, 77) };
            status.SetBounds(26, 70, 450, 48);
            status.Text = "Đang kiểm tra bản phát hành chính thức trên GitHub...";
            progress.SetBounds(26, 124, 450, 18);
            progress.Style = ProgressBarStyle.Marquee;
            action.SetBounds(326, 158, 150, 34);
            action.Text = "Đang kiểm tra...";
            action.Enabled = false;
            action.BackColor = Color.FromArgb(32, 112, 214);
            action.ForeColor = Color.White;
            action.FlatStyle = FlatStyle.Flat;
            action.FlatAppearance.BorderSize = 0;
            action.Click += async delegate
            {
                if (updateAvailable) await InstallAsync();
                else if (checkFailed) { action.Enabled = false; await CheckAsync(); }
                else Close();
            };
            Controls.AddRange(new Control[] { title, status, progress, action });
        }

        private async Task CheckAsync()
        {
            try
            {
                updateAvailable = false;
                checkFailed = false;
                action.Text = "Đang kiểm tra...";
                currentVersion = File.Exists(appPath) ? FileVersionInfo.GetVersionInfo(appPath).FileVersion == null ? new Version(0, 0) : new Version(FileVersionInfo.GetVersionInfo(appPath).FileVersion) : new Version(0, 0);
                string json = await DownloadTextAsync(ApiUrl);
                release = new JavaScriptSerializer().Deserialize<ReleaseInfo>(json);
                Version latest = ParseVersion(release.tag_name);
                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 0;
                if (latest <= currentVersion)
                {
                    status.Text = "Bạn đang dùng bản mới nhất: " + currentVersion;
                    action.Text = "Đóng";
                    action.Enabled = true;
                }
                else
                {
                    EnsureAssets(release);
                    updateAvailable = true;
                    status.Text = "Có bản mới " + latest + " (bản hiện tại " + currentVersion + ").";
                    action.Text = "CÀI BẢN MỚI";
                    action.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                checkFailed = true;
                progress.Style = ProgressBarStyle.Blocks;
                status.Text = "Không kiểm tra được cập nhật: " + Short(ex.Message);
                action.Text = "Thử lại";
                action.Enabled = true;
            }
        }

        private async Task InstallAsync()
        {
            action.Enabled = false;
            progress.Style = ProgressBarStyle.Marquee;
            string tempRoot = Path.Combine(Path.GetTempPath(), "AKMasterSocicalUpdate-" + Guid.NewGuid().ToString("N"));
            try
            {
                EnsureAssets(release);
                Directory.CreateDirectory(tempRoot);
                string zipPath = Path.Combine(tempRoot, "release.zip");
                string hashPath = Path.Combine(tempRoot, "release.sha256");
                status.Text = "Đang tải bản cập nhật từ GitHub...";
                await DownloadFileAsync(Asset(release, "AKMasterSocical-Clean.zip").browser_download_url, zipPath);
                await DownloadFileAsync(Asset(release, "AKMasterSocical-Clean.zip.sha256").browser_download_url, hashPath);
                status.Text = "Đang xác minh SHA-256...";
                VerifyHash(zipPath, File.ReadAllText(hashPath));
                string staging = Path.Combine(tempRoot, "staging");
                ExtractSafe(zipPath, staging);
                string source = Directory.Exists(Path.Combine(staging, "AKMasterSocical-Clean")) ? Path.Combine(staging, "AKMasterSocical-Clean") : staging;
                if (!File.Exists(Path.Combine(source, "AKMasterSocical.exe"))) throw new InvalidDataException("Gói cập nhật không có AKMasterSocical.exe.");
                status.Text = "Đang cài bản mới...";
                ApplyFiles(source, AppDomain.CurrentDomain.BaseDirectory);
                status.Text = "Cập nhật thành công. Đang mở lại ứng dụng...";
                Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
                await Task.Delay(700);
                Close();
            }
            catch (Exception ex)
            {
                updateAvailable = true;
                progress.Style = ProgressBarStyle.Blocks;
                status.Text = "Cập nhật thất bại: " + Short(ex.Message);
                action.Text = "Thử lại";
                action.Enabled = true;
            }
            finally
            {
                TryDelete(tempRoot);
            }
        }

        private static async Task<string> DownloadTextAsync(string url)
        {
            using (var wc = Client()) return await wc.DownloadStringTaskAsync(url);
        }

        private static async Task DownloadFileAsync(string url, string path)
        {
            using (var wc = Client()) await wc.DownloadFileTaskAsync(url, path);
        }

        private static WebClient Client()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "AKMasterSocical-Clean-Updater";
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        private static ReleaseAsset Asset(ReleaseInfo info, string name)
        {
            return (info.assets ?? new List<ReleaseAsset>()).FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureAssets(ReleaseInfo info)
        {
            if (info == null || info.prerelease) throw new InvalidDataException("Không tìm thấy bản phát hành ổn định.");
            if (Asset(info, "AKMasterSocical-Clean.zip") == null || Asset(info, "AKMasterSocical-Clean.zip.sha256") == null)
                throw new InvalidDataException("Bản phát hành thiếu gói ZIP hoặc file SHA-256.");
        }

        private static Version ParseVersion(string tag)
        {
            string value = (tag ?? "").Trim().TrimStart('v', 'V');
            Version version;
            if (!Version.TryParse(value, out version)) throw new InvalidDataException("Tag phiên bản không hợp lệ: " + tag);
            return version;
        }

        private static void VerifyHash(string file, string expectedText)
        {
            Match match = Regex.Match(expectedText ?? "", "[A-Fa-f0-9]{64}");
            if (!match.Success) throw new InvalidDataException("File SHA-256 không hợp lệ.");
            string actual;
            using (var stream = File.OpenRead(file))
            using (var sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!actual.Equals(match.Value, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 không khớp; đã hủy cập nhật.");
        }

        private static void ExtractSafe(string zipPath, string destination)
        {
            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Gói cập nhật chứa đường dẫn không an toàn.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open())
                    using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                }
            }
        }

        private static void ApplyFiles(string source, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.GetFullPath(Path.Combine(destination, relative));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Đường dẫn cập nhật không an toàn.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (string.Equals(Path.GetFileName(target), "Updater.exe", StringComparison.OrdinalIgnoreCase)) target = Path.Combine(destination, "Updater.exe.new");
                File.Copy(file, target, true);
            }
        }

        private static string Short(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Không rõ nguyên nhân";
            string first = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
            return first.Length > 120 ? first.Substring(0, 117) + "..." : first;
        }

        private static void TryDelete(string path)
        {
            try
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(path);
                if (target.StartsWith(temp + "AKMasterSocicalUpdate-", StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch { }
        }
    }
}
