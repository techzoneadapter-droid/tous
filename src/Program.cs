using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[assembly: AssemblyTitle("AKMasterSocical")]
[assembly: AssemblyDescription("Clean local-only country environment manager")]
[assembly: AssemblyProduct("AKMasterSocical Clean Edition")]
[assembly: AssemblyVersion("1.0.0.18")]
[assembly: AssemblyFileVersion("1.0.0.18")]

namespace AKMasterSocicalClean
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplyPendingUpdater();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void ApplyPendingUpdater()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string pending = Path.Combine(dir, "Updater.exe.new");
                string updater = Path.Combine(dir, "Updater.exe");
                if (!File.Exists(pending)) return;
                File.Copy(pending, updater, true);
                File.Delete(pending);
            }
            catch { }
        }
    }

    internal sealed class AccountRow
    {
        public bool Selected { get; set; }
        public string Uid { get; set; }
        public string Cookie { get; set; }
        public string Proxy { get; set; }
        public string Country { get; set; }
        public string Status { get; set; }

        public AccountRow()
        {
            Selected = true;
            Uid = "";
            Cookie = "";
            Proxy = "";
            Country = "VN";
            Status = "Chưa chạy";
        }
    }

    internal sealed class CountryProfile
    {
        public string Code { get; private set; }
        public string Name { get; private set; }
        public string Locale { get; private set; }
        public string TimeZone { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public CountryProfile(string code, string name, string locale, string timeZone, double latitude, double longitude)
        {
            Code = code;
            Name = name;
            Locale = locale;
            TimeZone = timeZone;
            Latitude = latitude;
            Longitude = longitude;
        }

        public override string ToString() { return Code + " - " + Name; }

        public static readonly CountryProfile[] All =
        {
            new CountryProfile("VN", "Việt Nam", "vi_VN", "Asia/Ho_Chi_Minh", 10.8231, 106.6297),
            new CountryProfile("US", "Hoa Kỳ", "en_US", "America/New_York", 40.7128, -74.0060),
            new CountryProfile("GB", "Vương quốc Anh", "en_GB", "Europe/London", 51.5074, -0.1278),
            new CountryProfile("CA", "Canada", "en_CA", "America/Toronto", 43.6532, -79.3832),
            new CountryProfile("AU", "Úc", "en_AU", "Australia/Sydney", -33.8688, 151.2093),
            new CountryProfile("SG", "Singapore", "en_SG", "Asia/Singapore", 1.3521, 103.8198),
            new CountryProfile("TH", "Thái Lan", "th_TH", "Asia/Bangkok", 13.7563, 100.5018),
            new CountryProfile("ID", "Indonesia", "id_ID", "Asia/Jakarta", -6.2088, 106.8456),
            new CountryProfile("PH", "Philippines", "en_PH", "Asia/Manila", 14.5995, 120.9842),
            new CountryProfile("JP", "Nhật Bản", "ja_JP", "Asia/Tokyo", 35.6762, 139.6503),
            new CountryProfile("KR", "Hàn Quốc", "ko_KR", "Asia/Seoul", 37.5665, 126.9780),
            new CountryProfile("DE", "Đức", "de_DE", "Europe/Berlin", 52.5200, 13.4050),
            new CountryProfile("FR", "Pháp", "fr_FR", "Europe/Paris", 48.8566, 2.3522)
        };

        public static CountryProfile Find(string code)
        {
            return All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ?? All[0];
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly BindingList<AccountRow> accounts = new BindingList<AccountRow>();
        private readonly ConcurrentDictionary<int, ChromeDriver> liveDrivers = new ConcurrentDictionary<int, ChromeDriver>();
        private readonly DataGridView grid = new DataGridView();
        private readonly RichTextBox log = new RichTextBox();
        private readonly ComboBox country = new ComboBox();
        private readonly NumericUpDown concurrency = new NumericUpDown();
        private readonly NumericUpDown dwellSeconds = new NumericUpDown();
        private readonly NumericUpDown delaySeconds = new NumericUpDown();
        private readonly CheckBox headless = new CheckBox();
        private readonly CheckBox verifyIp = new CheckBox();
        private readonly CheckBox keepBrowser = new CheckBox();
        private readonly Label summary = new Label();
        private readonly Button startButton = new Button();
        private readonly Button stopButton = new Button();
        private CancellationTokenSource cancellation;

        private static readonly Color Navy = Color.FromArgb(23, 43, 77);
        private static readonly Color Blue = Color.FromArgb(32, 112, 214);
        private static readonly Color Canvas = Color.FromArgb(244, 247, 251);

        public MainForm()
        {
            Text = "AKMasterSocical - Clean Edition";
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(960, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9F);
            Icon = TryLoadIcon();
            BuildUi();
            grid.DataSource = accounts;
            accounts.ListChanged += delegate { UpdateSummary(); };
            FormClosing += OnFormClosing;
            Log("Sẵn sàng. Bản sạch không có updater, telemetry, lệnh từ xa hoặc gửi dữ liệu ẩn.");
        }

        private Icon TryLoadIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
            }
            catch { return SystemIcons.Application; }
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Navy };
            var title = new Label { Text = "AKMasterSocical", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 18F), AutoSize = true, Left = 22, Top = 12 };
            var subtitle = new Label { Text = "Môi trường quốc gia cho phiên Facebook · xử lý cục bộ", ForeColor = Color.FromArgb(190, 207, 229), AutoSize = true, Left = 24, Top = 44 };
            var badge = new Label { Text = "CLEAN", ForeColor = Color.White, BackColor = Color.FromArgb(28, 160, 102), Font = new Font("Segoe UI Semibold", 9F), AutoSize = true, Padding = new Padding(8, 4, 8, 4), Left = 236, Top = 19 };
            header.Controls.AddRange(new Control[] { title, subtitle, badge });
            Controls.Add(header);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(14, 10, 10, 6), BackColor = Color.White, WrapContents = false };
            toolbar.Controls.Add(MakeButton("＋ Thêm", delegate { AddAccountDialog(); }, false));
            toolbar.Controls.Add(MakeButton("Nhập TXT", delegate { ImportAccounts(); }, false));
            toolbar.Controls.Add(MakeButton("Xuất kết quả", delegate { ExportAccounts(); }, false));
            toolbar.Controls.Add(MakeButton("Xóa dòng", delegate { DeleteSelectedRows(); }, false));
            toolbar.Controls.Add(MakeButton("Chọn tất cả", delegate { SetAllSelected(true); }, false));
            toolbar.Controls.Add(MakeButton("Bỏ chọn", delegate { SetAllSelected(false); }, false));
            Controls.Add(toolbar);

            var right = new Panel { Dock = DockStyle.Right, Width = 286, BackColor = Color.White, Padding = new Padding(18) };
            BuildSettings(right);
            Controls.Add(right);

            var logPanel = new Panel { Dock = DockStyle.Bottom, Height = 160, Padding = new Padding(14, 6, 14, 12), BackColor = Canvas };
            var logTitle = new Label { Dock = DockStyle.Top, Height = 24, Text = "NHẬT KÝ", ForeColor = Navy, Font = new Font("Segoe UI Semibold", 9F) };
            log.Dock = DockStyle.Fill;
            log.ReadOnly = true;
            log.BackColor = Color.FromArgb(20, 30, 45);
            log.ForeColor = Color.FromArgb(215, 226, 240);
            log.BorderStyle = BorderStyle.None;
            log.Font = new Font("Consolas", 9F);
            logPanel.Controls.Add(log);
            logPanel.Controls.Add(logTitle);
            Controls.Add(logPanel);

            var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 0), BackColor = Canvas };
            summary.Dock = DockStyle.Bottom;
            summary.Height = 28;
            summary.TextAlign = ContentAlignment.MiddleLeft;
            summary.ForeColor = Color.FromArgb(75, 88, 107);
            ConfigureGrid();
            gridPanel.Controls.Add(grid);
            gridPanel.Controls.Add(summary);
            Controls.Add(gridPanel);
        }

        private void ConfigureGrid()
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = 34;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Navy;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.ColumnHeadersHeight = 38;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 235, 250);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 36, 55);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "✓", Width = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Uid", HeaderText = "UID / NHÃN", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cookie", HeaderText = "COOKIE", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Proxy", HeaderText = "PROXY", Width = 150 });
            var c = new DataGridViewComboBoxColumn { DataPropertyName = "Country", HeaderText = "QUỐC GIA", Width = 90, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(CountryProfile.All.Select(x => x.Code).ToArray());
            grid.Columns.Add(c);
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "TRẠNG THÁI", Width = 180, ReadOnly = true });
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
        }

        private void BuildSettings(Panel host)
        {
            var heading = new Label { Text = "CẤU HÌNH CHẠY", Dock = DockStyle.Top, Height = 30, ForeColor = Navy, Font = new Font("Segoe UI Semibold", 11F) };
            host.Controls.Add(heading);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 410, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            flow.Controls.Add(MakeLabel("Quốc gia mặc định"));
            country.Width = 235;
            country.DropDownStyle = ComboBoxStyle.DropDownList;
            country.Items.AddRange(CountryProfile.All);
            country.SelectedIndex = 0;
            flow.Controls.Add(country);
            flow.Controls.Add(MakeLabel("Số luồng đồng thời"));
            concurrency.Width = 235; concurrency.Minimum = 1; concurrency.Maximum = 10; concurrency.Value = 1;
            flow.Controls.Add(concurrency);
            flow.Controls.Add(MakeLabel("Thời gian duy trì (giây)"));
            dwellSeconds.Width = 235; dwellSeconds.Minimum = 5; dwellSeconds.Maximum = 3600; dwellSeconds.Value = 20;
            flow.Controls.Add(dwellSeconds);
            flow.Controls.Add(MakeLabel("Trễ giữa tài khoản (giây)"));
            delaySeconds.Width = 235; delaySeconds.Minimum = 0; delaySeconds.Maximum = 300; delaySeconds.Value = 2;
            flow.Controls.Add(delaySeconds);
            headless.Text = "Chạy ẩn trình duyệt"; headless.Width = 235; headless.Height = 28;
            verifyIp.Text = "Kiểm tra IP bằng ipapi.co"; verifyIp.Width = 235; verifyIp.Height = 28; verifyIp.Checked = true;
            keepBrowser.Text = "Giữ trình duyệt sau khi chạy"; keepBrowser.Width = 235; keepBrowser.Height = 28;
            flow.Controls.Add(headless); flow.Controls.Add(verifyIp); flow.Controls.Add(keepBrowser);
            host.Controls.Add(flow);

            var notice = new Label
            {
                Dock = DockStyle.Top,
                Height = 82,
                Text = "Lưu ý: Facebook tự quyết định quốc gia tài khoản. App áp dụng proxy, ngôn ngữ, múi giờ và vị trí cho phiên Chrome; không vượt xác minh hay checkpoint.",
                ForeColor = Color.FromArgb(104, 82, 30),
                BackColor = Color.FromArgb(255, 248, 221),
                Padding = new Padding(10),
                AutoEllipsis = true
            };
            host.Controls.Add(notice);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 136, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            startButton.Text = "BẮT ĐẦU"; startButton.Width = 235; startButton.Height = 38; startButton.BackColor = Blue; startButton.ForeColor = Color.White; startButton.FlatStyle = FlatStyle.Flat; startButton.FlatAppearance.BorderSize = 0;
            stopButton.Text = "DỪNG"; stopButton.Width = 235; stopButton.Height = 36; stopButton.Enabled = false; stopButton.BackColor = Color.FromArgb(225, 70, 70); stopButton.ForeColor = Color.White; stopButton.FlatStyle = FlatStyle.Flat; stopButton.FlatAppearance.BorderSize = 0;
            var updateButton = new Button { Text = "KIỂM TRA CẬP NHẬT", Width = 235, Height = 34, BackColor = Color.White, ForeColor = Navy, FlatStyle = FlatStyle.Flat };
            updateButton.FlatAppearance.BorderColor = Color.FromArgb(180, 193, 210);
            startButton.Click += async delegate { await StartAsync(); };
            stopButton.Click += delegate { StopAll(); };
            updateButton.Click += delegate { LaunchUpdater(); };
            actions.Controls.Add(startButton); actions.Controls.Add(stopButton); actions.Controls.Add(updateButton);
            host.Controls.Add(actions);
        }

        private void LaunchUpdater()
        {
            string updater = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
            if (!File.Exists(updater))
            {
                MessageBox.Show(this, "Thiếu Updater.exe trong thư mục cài đặt.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (cancellation != null)
            {
                MessageBox.Show(this, "Hãy dừng tác vụ đang chạy trước khi cập nhật.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var answer = MessageBox.Show(this, "Ứng dụng sẽ đóng để kiểm tra và cài bản mới từ GitHub chính thức. Tiếp tục?", "Cập nhật", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            Process.Start(new ProcessStartInfo(updater, "\"" + Application.ExecutablePath + "\"") { UseShellExecute = true });
            Close();
        }

        private Label MakeLabel(string text)
        {
            return new Label { Text = text, Width = 235, Height = 24, Margin = new Padding(0, 8, 0, 0), ForeColor = Color.FromArgb(64, 75, 91) };
        }

        private Button MakeButton(string text, EventHandler action, bool primary)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(4, 0, 4, 0), FlatStyle = FlatStyle.Flat, BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Navy };
            b.FlatAppearance.BorderColor = Color.FromArgb(205, 214, 225);
            b.Click += action;
            return b;
        }

        private void AddAccountDialog()
        {
            using (var form = new Form { Text = "Thêm tài khoản", Width = 600, Height = 390, StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false, MaximizeBox = false })
            {
                var uid = AddField(form, "UID / nhãn", 20, 20, false);
                var cookie = AddField(form, "Cookie Facebook", 20, 82, true);
                var proxy = AddField(form, "Proxy (http://host:port hoặc socks5://host:port)", 20, 204, false);
                var cc = new ComboBox { Left = 20, Top = 286, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
                cc.Items.AddRange(CountryProfile.All);
                cc.SelectedIndex = 0;
                var ok = new Button { Text = "Thêm", Left = 450, Top = 286, Width = 100, Height = 32, DialogResult = DialogResult.OK, BackColor = Blue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                form.Controls.Add(new Label { Text = "Quốc gia", Left = 20, Top = 266, AutoSize = true });
                form.Controls.Add(cc); form.Controls.Add(ok); form.AcceptButton = ok;
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    string derivedUid = string.IsNullOrWhiteSpace(uid.Text) ? ExtractUid(cookie.Text) : uid.Text.Trim();
                    accounts.Add(new AccountRow { Uid = derivedUid, Cookie = cookie.Text.Trim(), Proxy = proxy.Text.Trim(), Country = ((CountryProfile)cc.SelectedItem).Code, Status = "Chưa chạy" });
                }
            }
        }

        private TextBox AddField(Form form, string label, int left, int top, bool multiline)
        {
            form.Controls.Add(new Label { Text = label, Left = left, Top = top, AutoSize = true });
            var box = new TextBox { Left = left, Top = top + 22, Width = 530, Height = multiline ? 92 : 26, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            form.Controls.Add(box);
            return box;
        }

        private void ImportAccounts()
        {
            using (var dialog = new OpenFileDialog { Filter = "Text/CSV|*.txt;*.csv|Tất cả file|*.*", Title = "Nhập tài khoản" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                int added = 0;
                foreach (string raw in File.ReadAllLines(dialog.FileName, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] p = line.Split(new[] { '|' }, 4);
                    var row = new AccountRow { Country = ((CountryProfile)country.SelectedItem).Code };
                    if (p.Length == 1) { row.Cookie = p[0]; row.Uid = ExtractUid(p[0]); }
                    else { row.Uid = p[0].Trim(); row.Cookie = p[1].Trim(); }
                    if (p.Length >= 3) row.Proxy = p[2].Trim();
                    if (p.Length >= 4 && CountryProfile.All.Any(x => x.Code.Equals(p[3].Trim(), StringComparison.OrdinalIgnoreCase))) row.Country = p[3].Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(row.Uid)) row.Uid = "TK-" + (accounts.Count + 1).ToString(CultureInfo.InvariantCulture);
                    accounts.Add(row); added++;
                }
                Log("Đã nhập " + added + " tài khoản. Cookie chỉ nằm trong bộ nhớ của app.");
            }
        }

        private void ExportAccounts()
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV UTF-8|*.csv", FileName = "ket-qua.csv", Title = "Xuất kết quả (không chứa cookie)" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var lines = new List<string> { "UID,Proxy,Country,Status" };
                foreach (var a in accounts) lines.Add(Csv(a.Uid) + "," + Csv(RedactProxy(a.Proxy)) + "," + Csv(a.Country) + "," + Csv(a.Status));
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                Log("Đã xuất kết quả không chứa cookie: " + dialog.FileName);
            }
        }

        private static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }

        private void DeleteSelectedRows()
        {
            var rows = grid.SelectedRows.Cast<DataGridViewRow>().Select(x => x.DataBoundItem as AccountRow).Where(x => x != null).ToList();
            foreach (var row in rows) accounts.Remove(row);
        }

        private void SetAllSelected(bool value)
        {
            foreach (var row in accounts) row.Selected = value;
            grid.Refresh(); UpdateSummary();
        }

        private async Task StartAsync()
        {
            grid.EndEdit();
            var jobs = accounts.Where(x => x.Selected).ToList();
            if (jobs.Count == 0) { MessageBox.Show(this, "Hãy chọn ít nhất một tài khoản.", "AKMasterSocical", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromedriver.exe");
            if (!File.Exists(driverPath)) { MessageBox.Show(this, "Thiếu chromedriver.exe chính chủ trong thư mục app.", "Thiếu thành phần", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            cancellation = new CancellationTokenSource();
            startButton.Enabled = false; stopButton.Enabled = true;
            int parallel = (int)concurrency.Value;
            int dwell = (int)dwellSeconds.Value;
            int delay = (int)delaySeconds.Value;
            bool runHeadless = headless.Checked;
            bool checkIp = verifyIp.Checked;
            bool leaveOpen = keepBrowser.Checked;
            var defaultCountry = (CountryProfile)country.SelectedItem;
            Log("Bắt đầu " + jobs.Count + " tài khoản, " + parallel + " luồng.");
            var gate = new SemaphoreSlim(parallel, parallel);
            var tasks = new List<Task>();
            for (int i = 0; i < jobs.Count; i++)
            {
                var row = jobs[i];
                int jobId = i + 1;
                if (string.IsNullOrWhiteSpace(row.Country)) row.Country = defaultCountry.Code;
                tasks.Add(RunGuardedAsync(jobId, row, gate, dwell, delay * i, runHeadless, checkIp, leaveOpen, cancellation.Token));
            }
            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { Log("Đã dừng theo yêu cầu."); }
            finally
            {
                gate.Dispose();
                startButton.Enabled = true; stopButton.Enabled = false;
                cancellation.Dispose(); cancellation = null;
                UpdateSummary();
                Log("Hoàn tất phiên xử lý.");
            }
        }

        private async Task RunGuardedAsync(int id, AccountRow row, SemaphoreSlim gate, int dwell, int delay, bool runHeadless, bool checkIp, bool leaveOpen, CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), token);
            await gate.WaitAsync(token);
            try { await Task.Run(delegate { RunAccount(id, row, dwell, runHeadless, checkIp, leaveOpen, token); }, token); }
            finally { gate.Release(); }
        }

        private void RunAccount(int id, AccountRow row, int dwell, bool runHeadless, bool checkIp, bool leaveOpen, CancellationToken token)
        {
            ChromeDriver driver = null;
            string profilePath = Path.Combine(Path.GetTempPath(), "AKMasterSocicalClean", Guid.NewGuid().ToString("N"));
            try
            {
                SetStatus(row, "Đang khởi tạo");
                Directory.CreateDirectory(profilePath);
                CountryProfile cp = CountryProfile.Find(row.Country);
                var service = ChromeDriverService.CreateDefaultService(AppDomain.CurrentDomain.BaseDirectory, "chromedriver.exe");
                service.HideCommandPromptWindow = true;
                var options = new ChromeOptions();
                options.AddArgument("--user-data-dir=" + profilePath);
                options.AddArgument("--lang=" + cp.Locale.Replace('_', '-'));
                options.AddArgument("--disable-background-networking");
                options.AddArgument("--disable-component-update");
                options.AddArgument("--disable-default-apps");
                options.AddArgument("--disable-sync");
                options.AddArgument("--no-first-run");
                options.AddArgument("--window-size=1280,900");
                if (runHeadless) options.AddArgument("--headless=new");
                if (!string.IsNullOrWhiteSpace(row.Proxy)) options.AddArgument("--proxy-server=" + NormalizeProxy(row.Proxy));
                driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(60));
                liveDrivers[id] = driver;
                token.ThrowIfCancellationRequested();

                TrySetEnvironment(driver, cp);
                if (checkIp)
                {
                    SetStatus(row, "Đang kiểm tra IP");
                    driver.Navigate().GoToUrl("https://ipapi.co/country/");
                    string actual = (driver.FindElement(By.TagName("body")).Text ?? "").Trim().ToUpperInvariant();
                    if (actual.Length == 2 && !actual.Equals(cp.Code, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("IP hiện tại là " + actual + ", không phải " + cp.Code + ". Hãy kiểm tra proxy.");
                }

                SetStatus(row, "Đang mở Facebook");
                driver.Navigate().GoToUrl("https://www.facebook.com/");
                token.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(row.Cookie))
                {
                    int count = AddFacebookCookies(driver, row.Cookie);
                    if (count == 0) throw new InvalidOperationException("Cookie không đúng định dạng name=value; ...");
                    driver.Navigate().GoToUrl("https://www.facebook.com/?locale=" + Uri.EscapeDataString(cp.Locale));
                }
                else
                {
                    LogSafe(row, "Không có cookie; chờ đăng nhập thủ công trong cửa sổ Chrome.");
                }

                SetStatus(row, "Đang duy trì phiên " + cp.Code);
                for (int second = 0; second < dwell; second++)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(1000);
                }
                SetStatus(row, leaveOpen ? "Thành công · Chrome đang mở" : "Thành công");
                LogSafe(row, "Đã áp dụng locale " + cp.Locale + ", múi giờ " + cp.TimeZone + " và vị trí " + cp.Code + ".");
                if (leaveOpen) { liveDrivers.TryRemove(id, out driver); driver = null; }
            }
            catch (OperationCanceledException) { SetStatus(row, "Đã dừng"); }
            catch (Exception ex)
            {
                SetStatus(row, "Lỗi: " + ShortMessage(ex.Message));
                LogSafe(row, "LỖI: " + ShortMessage(ex.Message));
            }
            finally
            {
                ChromeDriver tracked;
                liveDrivers.TryRemove(id, out tracked);
                if (driver != null) { try { driver.Quit(); } catch { } try { driver.Dispose(); } catch { } }
                if (!leaveOpen) TryDeleteProfile(profilePath);
            }
        }

        private static void TrySetEnvironment(ChromeDriver driver, CountryProfile cp)
        {
            try
            {
                driver.ExecuteCdpCommand("Emulation.setTimezoneOverride", new Dictionary<string, object> { { "timezoneId", cp.TimeZone } });
                driver.ExecuteCdpCommand("Emulation.setGeolocationOverride", new Dictionary<string, object>
                {
                    { "latitude", cp.Latitude }, { "longitude", cp.Longitude }, { "accuracy", 20 }
                });
                driver.ExecuteCdpCommand("Browser.grantPermissions", new Dictionary<string, object>
                {
                    { "origin", "https://www.facebook.com" }, { "permissions", new[] { "geolocation" } }
                });
            }
            catch { }
        }

        private static int AddFacebookCookies(ChromeDriver driver, string header)
        {
            int count = 0;
            foreach (string part in header.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string name = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                if (name.Length == 0) continue;
                try
                {
                    driver.Manage().Cookies.AddCookie(new Cookie(name, value, ".facebook.com", "/", null));
                    count++;
                }
                catch { }
            }
            return count;
        }

        private static string NormalizeProxy(string proxy)
        {
            string value = proxy.Trim();
            if (value.Contains("@")) throw new InvalidOperationException("Chrome không hỗ trợ proxy có user/password qua dòng lệnh. Hãy dùng proxy whitelist IP.");
            if (!value.Contains("://")) value = "http://" + value;
            Uri parsed;
            if (!Uri.TryCreate(value, UriKind.Absolute, out parsed) || string.IsNullOrWhiteSpace(parsed.Host) || parsed.Port <= 0) throw new InvalidOperationException("Proxy không hợp lệ.");
            string scheme = parsed.Scheme.ToLowerInvariant();
            if (scheme != "http" && scheme != "https" && scheme != "socks5" && scheme != "socks4") throw new InvalidOperationException("Chỉ hỗ trợ HTTP, HTTPS, SOCKS4 hoặc SOCKS5.");
            return value;
        }

        private static string ExtractUid(string cookie)
        {
            foreach (string part in (cookie ?? "").Split(';'))
            {
                string s = part.Trim();
                if (s.StartsWith("c_user=", StringComparison.OrdinalIgnoreCase)) return s.Substring(7).Trim();
            }
            return "";
        }

        private void StopAll()
        {
            if (cancellation != null) cancellation.Cancel();
            foreach (var item in liveDrivers.ToArray())
            {
                ChromeDriver driver;
                if (liveDrivers.TryRemove(item.Key, out driver)) { try { driver.Quit(); } catch { } }
            }
            stopButton.Enabled = false;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e) { StopAll(); }

        private void SetStatus(AccountRow row, string status)
        {
            if (InvokeRequired) { BeginInvoke(new Action<AccountRow, string>(SetStatus), row, status); return; }
            row.Status = status; grid.Refresh(); UpdateSummary();
        }

        private void LogSafe(AccountRow row, string message) { Log("[" + (string.IsNullOrWhiteSpace(row.Uid) ? "Tài khoản" : row.Uid) + "] " + message); }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
            log.SelectionStart = log.TextLength; log.ScrollToCaret();
        }

        private void UpdateSummary()
        {
            if (InvokeRequired) { BeginInvoke(new Action(UpdateSummary)); return; }
            int selected = accounts.Count(x => x.Selected);
            int success = accounts.Count(x => x.Status != null && x.Status.StartsWith("Thành công", StringComparison.Ordinal));
            int failed = accounts.Count(x => x.Status != null && x.Status.StartsWith("Lỗi", StringComparison.Ordinal));
            summary.Text = "Tổng: " + accounts.Count + "   ·   Đã chọn: " + selected + "   ·   Thành công: " + success + "   ·   Lỗi: " + failed;
        }

        private static string RedactProxy(string proxy)
        {
            if (string.IsNullOrWhiteSpace(proxy)) return "";
            int at = proxy.LastIndexOf('@');
            return at >= 0 ? "***@" + proxy.Substring(at + 1) : proxy;
        }

        private static string ShortMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Không rõ nguyên nhân";
            string first = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? message;
            return first.Length > 120 ? first.Substring(0, 117) + "..." : first;
        }

        private static void TryDeleteProfile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(Path.Combine(Path.GetTempPath(), "AKMasterSocicalClean"), StringComparison.OrdinalIgnoreCase)) return;
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
