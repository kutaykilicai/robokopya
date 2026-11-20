using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RoboKopya.Cekirdek;

namespace RoboKopya;

/// <summary>Robocopy'yi gizli çalıştırır, ilerlemeyi süreç G/Ç sayaçlarından ve çıktı satırlarından izler.</summary>
public sealed class Motor
{
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS { public ulong R, W, O, RB, WB, OB; }
    [DllImport("kernel32.dll")] static extern bool GetProcessIoCounters(IntPtr h, out IO_COUNTERS c);
    [DllImport("kernel32.dll")] static extern uint GetOEMCP();
    [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr h);

    readonly object _k = new();
    Process? _p;
    long _oncekiIsler, _isToplam, _isLog;
    string? _bekleyenHata;

    public long ToplamBayt { get; private set; }
    public long ToplamOge { get; private set; }
    public long BitenOge { get; private set; }
    public string SuAnki { get; private set; } = "";
    public bool Listeleniyor { get; private set; } = true;
    public bool Duraklatildi { get; private set; }
    public bool Iptal { get; private set; }
    public bool Bitti { get; private set; }
    public int HataliIs { get; private set; }
    public List<string> Hatalar { get; } = new();

    static Encoding Oem()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { return Encoding.GetEncoding((int)GetOEMCP()); } catch { return Encoding.Default; }
    }

    public long KopyalananBayt()
    {
        lock (_k)
        {
            if (Bitti && HataliIs == 0 && !Iptal) return ToplamBayt;
            long su = _isLog;
            try { if (_p != null && !_p.HasExited && GetProcessIoCounters(_p.Handle, out var c)) su = Math.Max(su, (long)c.WB); } catch { }
            return Math.Min(ToplamBayt, _oncekiIsler + Math.Min(su, _isToplam));
        }
    }

    public async Task Calistir(IReadOnlyList<KopyaIsi> isler)
    {
        var enc = Oem();
        var toplamlar = new long[isler.Count];
        for (int i = 0; i < isler.Count && !Iptal; i++)
        {
            await Robocopy(isler[i].Argumanlar(true), enc, s =>
            {
                if (Cikti.DosyaSatiri(s, out var b, out _)) lock (_k) { toplamlar[i] += b; ToplamBayt += b; ToplamOge++; }
            });
        }
        lock (_k) Listeleniyor = false;
        for (int i = 0; i < isler.Count && !Iptal; i++)
        {
            lock (_k) { _isToplam = toplamlar[i]; _isLog = 0; _bekleyenHata = null; }
            int kod = await Robocopy(isler[i].Argumanlar(false), enc, Satir);
            lock (_k)
            {
                if (kod >= 8 && !Iptal) HataliIs++;
                _oncekiIsler += _isToplam; _isLog = 0; _isToplam = 0;
            }
        }
        lock (_k) { _p = null; Bitti = true; }
    }

    void Satir(string s)
    {
        lock (_k)
        {
            if (Cikti.DosyaSatiri(s, out var b, out var yol))
            {
                BitenOge = Math.Min(ToplamOge, BitenOge + 1); _isLog += b; SuAnki = Planlayici.Ad(yol); _bekleyenHata = null;
            }
            else if (Cikti.HataSatiri(s)) { Hatalar.Add(s.Trim()); _bekleyenHata = s; }
            else if (_bekleyenHata != null && s.Trim().Length > 0) { Hatalar[^1] += " " + s.Trim(); _bekleyenHata = null; }
        }
    }

    async Task<int> Robocopy(string args, Encoding enc, Action<string> satir)
    {
        var psi = new ProcessStartInfo("robocopy.exe", args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = enc,
        };
        var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) satir(e.Data); };
        p.ErrorDataReceived += (_, e) => { };
        lock (_k) { if (Iptal) return 16; p.Start(); _p = p; }
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        lock (_k) _p = null;
        return p.ExitCode;
    }

    public void DuraklatDevam()
    {
        lock (_k)
        {
            if (_p == null || _p.HasExited) return;
            if (Duraklatildi) NtResumeProcess(_p.Handle); else NtSuspendProcess(_p.Handle);
            Duraklatildi = !Duraklatildi;
        }
    }

    public void IptalEt()
    {
        lock (_k)
        {
            Iptal = true;
            if (_p == null || _p.HasExited) return;
            if (Duraklatildi) { NtResumeProcess(_p.Handle); Duraklatildi = false; }
            try { _p.Kill(); } catch { }
        }
    }
}
