using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NexBoostGaming;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GamingForm());
    }
}

internal sealed class GameEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public override string ToString() => Name;
}

internal static class Metrics
{
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; public ulong Value => ((ulong)High << 32) | Low; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length; public uint MemoryLoad; public ulong TotalPhys; public ulong AvailPhys;
        public ulong TotalPageFile; public ulong AvailPageFile; public ulong TotalVirtual; public ulong AvailVirtual; public ulong AvailExtendedVirtual;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    private static ulong _lastIdle, _lastTotal;
    public static int Cpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return -1;
        ulong idleNow = idle.Value, totalNow = kernel.Value + user.Value;
        ulong idleDiff = idleNow >= _lastIdle ? idleNow - _lastIdle : 0;
        ulong totalDiff = totalNow >= _lastTotal ? totalNow - _lastTotal : 0;
        _lastIdle = idleNow; _lastTotal = totalNow;
        if (totalDiff == 0) return -1;
        return Math.Clamp((int)Math.Round(100.0 * (totalDiff - Math.Min(idleDiff, totalDiff)) / totalDiff), 0, 100);
    }
    public static int Ram()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (int)status.MemoryLoad : -1;
    }
}

internal sealed class OverlayForm : Form
{
    private readonly Label _cpu, _ram, _fps;
    private Point _dragOrigin;
    private bool _dragging;
    public OverlayForm()
    {
        Text = "NexBoost Gaming Overlay";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(13, 21, 34);
        Opacity = .94;
        Size = new Size(370, 64);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(Screen.PrimaryScreen?.WorkingArea.Right - 390 ?? 100, 20);
        var header = new Label { Text = "NEXBOOST  |", ForeColor = Color.FromArgb(76, 211, 255), Font = new Font("Bahnschrift SemiCondensed", 11, FontStyle.Bold), AutoSize = true, Location = new Point(12, 22) };
        _cpu = MakeLabel(125); _ram = MakeLabel(202); _fps = MakeLabel(283);
        Controls.AddRange(new Control[] { header, _cpu, _ram, _fps });
        foreach (Control c in Controls) { c.MouseDown += DragDown; c.MouseMove += DragMove; c.MouseUp += (_, _) => _dragging = false; }
        MouseDown += DragDown; MouseMove += DragMove; MouseUp += (_, _) => _dragging = false;
        var tip = new ToolTip(); tip.SetToolTip(header, "Arrasta para mover. FPS necessita de uma fonte de captura externa.");
    }
    private static Label MakeLabel(int x) => new() { ForeColor = Color.White, Font = new Font("Bahnschrift SemiCondensed", 11, FontStyle.Bold), AutoSize = true, Location = new Point(x, 22) };
    public void UpdateMetrics(int cpu, int ram) { _cpu.Text = $"CPU {Fmt(cpu)}"; _ram.Text = $"RAM {Fmt(ram)}"; _fps.Text = "FPS N/D"; }
    private static string Fmt(int value) => value < 0 ? "N/D" : $"{value}%";
    private void DragDown(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _dragging = true; _dragOrigin = Cursor.Position; } }
    private void DragMove(object? sender, MouseEventArgs e) { if (!_dragging) return; var p = Cursor.Position; Location = new Point(Location.X + p.X - _dragOrigin.X, Location.Y + p.Y - _dragOrigin.Y); _dragOrigin = p; }
}

internal sealed class GamingForm : Form
{
    private readonly Color bg = Color.FromArgb(13, 20, 32), panel = Color.FromArgb(28, 42, 61), accent = Color.FromArgb(42, 170, 242);
    private readonly List<GameEntry> _games = new();
    private readonly ListBox _list = new();
    private readonly Label _cpu = new(), _ram = new(), _gpu = new(), _status = new();
    private readonly Button _boostButton;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexBoostGaming", "games.json");
    private readonly string _recoveryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexBoostGaming", "previous-power-plan.txt");
    private OverlayForm? _overlay;
    private string? _previousPlan;
    private bool _boosted;
    private int _cpuValue = -1, _ramValue = -1;
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public GamingForm()
    {
        Text = "NexBoost Gaming"; ClientSize = new Size(850, 550); MinimumSize = new Size(790, 530);
        StartPosition = FormStartPosition.CenterScreen; BackColor = bg; ForeColor = Color.White;
        Font = new Font("Segoe UI", 10); Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var title = new Label { Text = "⚡ NEXBOOST GAMING", Font = new Font("Bahnschrift SemiCondensed", 25, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(34, 26) };
        var subtitle = new Label { Text = "GAME PERFORMANCE CENTER  •  SAFE BOOST", Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(128, 202, 235), AutoSize = true, Location = new Point(38, 78) };
        Controls.Add(title); Controls.Add(subtitle);
        var gamesPanel = Card(35, 116, 420, 295);
        gamesPanel.Controls.Add(new Label { Text = "OS TEUS JOGOS", Location = new Point(18, 15), Size = new Size(350, 30), Font = new Font("Bahnschrift SemiCondensed", 16, FontStyle.Bold), ForeColor = Color.White });
        _list.Location = new Point(18, 54); _list.Size = new Size(382, 170); _list.BackColor = Color.FromArgb(17, 28, 43); _list.ForeColor = Color.White; _list.BorderStyle = BorderStyle.None; _list.Font = new Font("Segoe UI", 11);
        gamesPanel.Controls.Add(_list);
        var add = Btn("+ ADICIONAR JOGO", 18, 239, 181, 40); add.Click += AddGame;
        var remove = Btn("REMOVER", 211, 239, 90, 40); remove.Click += (_, _) => RemoveGame();
        var launch = Btn("ABRIR JOGO", 309, 239, 91, 40); launch.Click += (_, _) => LaunchGame();
        gamesPanel.Controls.AddRange(new Control[] { add, remove, launch });
        var metricsPanel = Card(474, 116, 340, 180);
        metricsPanel.Controls.Add(new Label { Text = "MONITORIZAÇÃO", Location = new Point(18, 14), Size = new Size(290, 30), Font = new Font("Bahnschrift SemiCondensed", 16, FontStyle.Bold) });
        SetupMetric(_cpu, "CPU", 18, metricsPanel); SetupMetric(_ram, "RAM", 125, metricsPanel); SetupMetric(_gpu, "GPU", 232, metricsPanel);
        var overlayButton = Btn("MOSTRAR / OCULTAR OVERLAY", 18, 120, 304, 43); overlayButton.Click += (_, _) => ToggleOverlay(); metricsPanel.Controls.Add(overlayButton);
        _boostButton = Btn("⚡ BOOST GAME", 474, 314, 340, 96); _boostButton.Font = new Font("Bahnschrift SemiCondensed", 22, FontStyle.Bold); _boostButton.Click += async (_, _) => await ToggleBoost();
        _status.Text = "Pronto. Seleciona um jogo e ativa o Boost."; _status.ForeColor = Color.FromArgb(155, 199, 224); _status.Location = new Point(38, 432); _status.Size = new Size(775, 65);
        Controls.AddRange(new Control[] { gamesPanel, metricsPanel, _boostButton, _status });
        LoadGames();
        if (File.Exists(_recoveryPath)) _status.Text = "Foi encontrado um Boost anterior. Usa BOOST para recuperar o plano guardado antes de otimizar.";
        _timer.Tick += (_, _) => TickMetrics(); _timer.Start(); TickMetrics();
        FormClosing += (_, _) => { _timer.Stop(); if (_boosted) RestorePlan(); _overlay?.Close(); };
    }
    private Panel Card(int x, int y, int w, int h) => new() { Location = new Point(x, y), Size = new Size(w, h), BackColor = panel };
    private Button Btn(string text, int x, int y, int w, int h)
    {
        var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = Color.White, Font = new Font("Bahnschrift SemiCondensed", 11, FontStyle.Bold), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0; return b;
    }
    private static void SetupMetric(Label label, string title, int x, Panel host)
    {
        label.Text = title + "\nN/D"; label.Location = new Point(x, 57); label.Size = new Size(100, 60);
        label.Font = new Font("Bahnschrift SemiCondensed", 14, FontStyle.Bold); label.ForeColor = Color.White; host.Controls.Add(label);
    }
    private void TickMetrics()
    {
        _cpuValue = Metrics.Cpu(); _ramValue = Metrics.Ram();
        _cpu.Text = $"CPU\n{Display(_cpuValue)}"; _ram.Text = $"RAM\n{Display(_ramValue)}"; _gpu.Text = "GPU\nN/D";
        _overlay?.UpdateMetrics(_cpuValue, _ramValue);
    }
    private static string Display(int x) => x < 0 ? "N/D" : x + "%";
    private void ToggleOverlay()
    {
        if (_overlay is { IsDisposed: false }) { _overlay.Close(); _overlay = null; return; }
        _overlay = new OverlayForm(); _overlay.UpdateMetrics(_cpuValue, _ramValue); _overlay.Show(this);
    }
    private void AddGame(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "Escolher executável do jogo", Filter = "Executáveis (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (_games.Any(g => string.Equals(g.Path, dialog.FileName, StringComparison.OrdinalIgnoreCase))) { _status.Text = "Esse jogo já foi adicionado."; return; }
        var game = new GameEntry { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Path = dialog.FileName };
        _games.Add(game); _list.Items.Add(game); _list.SelectedItem = game; SaveGames();
    }
    private void RemoveGame()
    {
        if (_list.SelectedItem is not GameEntry game) return;
        _games.Remove(game); _list.Items.Remove(game); SaveGames();
    }
    private void LaunchGame()
    {
        if (_list.SelectedItem is not GameEntry game) { MessageBox.Show("Seleciona primeiro um jogo."); return; }
        if (!File.Exists(game.Path)) { MessageBox.Show("O executável já não existe neste caminho."); return; }
        try { Process.Start(new ProcessStartInfo(game.Path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(game.Path) ?? "" }); _status.Text = "Jogo iniciado: " + game.Name; }
        catch (Exception ex) { MessageBox.Show("Não foi possível abrir o jogo: " + ex.Message); }
    }
    private async Task ToggleBoost()
    {
        _boostButton.Enabled = false;
        try
        {
            if (_boosted || File.Exists(_recoveryPath)) { await Task.Run(RestorePlan); return; }
            if (_list.SelectedItem is not GameEntry) { MessageBox.Show("Seleciona um jogo antes de ativar o Boost."); return; }
            string result = await RunPowerCfg("/getactivescheme");
            var match = Regex.Match(result, @"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
            if (!match.Success) { MessageBox.Show("Não foi possível identificar o plano de energia atual. O Boost não foi aplicado."); return; }
            _previousPlan = match.Value;
            Directory.CreateDirectory(Path.GetDirectoryName(_recoveryPath)!);
            File.WriteAllText(_recoveryPath, _previousPlan);
            try
            {
                await RunPowerCfg("/setactive " + HighPerformance);
                string check = await RunPowerCfg("/getactivescheme");
                if (!check.Contains(HighPerformance, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("O plano Alto Desempenho não está disponível ou não pôde ser ativado.");
                _boosted = true; _boostButton.Text = "↩ REVERTER BOOST";
                _status.Text = "Boost ativo: plano Alto Desempenho. Pode não aumentar FPS; reverte quando terminares.";
            }
            catch (Exception ex) { await Task.Run(RestorePlan); MessageBox.Show(ex.Message); }
        }
        finally { _boostButton.Enabled = true; }
    }
    private void RestorePlan()
    {
        try
        {
            string? guid = _previousPlan;
            if (string.IsNullOrWhiteSpace(guid) && File.Exists(_recoveryPath)) guid = File.ReadAllText(_recoveryPath).Trim();
            if (!string.IsNullOrWhiteSpace(guid) && Regex.IsMatch(guid, @"^[0-9a-fA-F-]{36}$"))
            {
                var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive " + guid) { UseShellExecute = false, CreateNoWindow = true });
                p?.WaitForExit(5000);
                if (p?.ExitCode != 0) throw new InvalidOperationException("Não foi possível restaurar o plano anterior.");
            }
            if (File.Exists(_recoveryPath)) File.Delete(_recoveryPath);
            _previousPlan = null; _boosted = false;
            if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { _boostButton.Text = "⚡ BOOST GAME"; _status.Text = "Plano de energia anterior restaurado."; });
        }
        catch (Exception ex)
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { _status.Text = "ATENÇÃO: recuperação pendente — " + ex.Message; });
        }
    }
    private static async Task<string> RunPowerCfg(string args)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo("powercfg.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        p.Start();
        string stdout = await p.StandardOutput.ReadToEndAsync();
        string stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0) throw new InvalidOperationException("powercfg: " + stderr);
        return stdout;
    }
    private void LoadGames()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var games = JsonSerializer.Deserialize<List<GameEntry>>(File.ReadAllText(_settingsPath)) ?? new();
            foreach (var game in games.Where(g => !string.IsNullOrWhiteSpace(g.Path))) { _games.Add(game); _list.Items.Add(game); }
        }
        catch { _status.Text = "Não foi possível carregar a lista de jogos."; }
    }
    private void SaveGames()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { MessageBox.Show("Erro ao guardar jogos: " + ex.Message); }
    }
}
