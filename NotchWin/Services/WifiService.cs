using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace NotchWin.Services
{
    public class WifiNetworkItem : ObservableObject
    {
        public string Ssid { get; set; } = string.Empty;
        public int SignalBars { get; set; } = 4;
        public int SignalPercentage { get; set; } = 100;
        public bool IsConnected { get; set; }
        public bool IsSecured { get; set; } = true;
        public bool IsSaved { get; set; }
        public string SecurityType { get; set; } = "WPA2";
        public string StatusText => IsConnected ? "Terhubung" : (IsSaved ? "Tersimpan" : (IsSecured ? "Diamankan" : "Terbuka"));
    }

    public class WifiService : ObservableObject
    {
        private WiFiAdapter? _adapter;
        private bool _isScanning;
        private bool _initialized;

        public ObservableCollection<WifiNetworkItem> Networks { get; } = new();

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    NotifyStateChanged();
                }
            }
        }

        public bool ShowSearching => IsScanning;
        public bool ShowEmpty => !IsScanning && Networks.Count == 0;
        public bool ShowList => !IsScanning && Networks.Count > 0;

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(ShowSearching));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowList));
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            try
            {
                var access = await WiFiAdapter.RequestAccessAsync();
                if (access == WiFiAccessStatus.Allowed)
                {
                    var adapters = await WiFiAdapter.FindAllAdaptersAsync();
                    if (adapters.Count > 0)
                    {
                        _adapter = adapters[0];
                    }
                }
                _initialized = true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WifiService: Failed to initialize WiFiAdapter");
            }
        }

        public async Task RefreshNetworksAsync()
        {
            if (IsScanning) return;
            App.Current?.Dispatcher?.Invoke(() =>
            {
                Networks.Clear();
                NotifyStateChanged();
            });
            IsScanning = true;
            NotifyStateChanged();
            try
            {
                await Task.Run(async () =>
                {
                    string connectedSsid = GetConnectedSsid();
                    var savedProfiles = GetSavedProfiles();
                    var list = new List<WifiNetworkItem>();

                    if (!_initialized)
                    {
                        await InitializeAsync();
                    }

                    if (_adapter != null)
                    {
                        try
                        {
                            await _adapter.ScanAsync();
                            foreach (var net in _adapter.NetworkReport.AvailableNetworks)
                            {
                                if (string.IsNullOrWhiteSpace(net.Ssid)) continue;
                                if (list.Exists(x => x.Ssid == net.Ssid)) continue;

                                bool isConn = !string.IsNullOrEmpty(connectedSsid) &&
                                              string.Equals(connectedSsid, net.Ssid, StringComparison.OrdinalIgnoreCase);

                                bool isSaved = savedProfiles.Contains(net.Ssid);

                                bool isSec = net.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.None &&
                                             net.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.Open80211;

                                list.Add(new WifiNetworkItem
                                {
                                    Ssid = net.Ssid,
                                    SignalBars = Math.Clamp((int)net.SignalBars, 1, 4),
                                    SignalPercentage = Math.Clamp((int)net.SignalBars * 25, 10, 100),
                                    IsConnected = isConn,
                                    IsSecured = isSec,
                                    IsSaved = isSaved,
                                    SecurityType = net.SecuritySettings.NetworkAuthenticationType.ToString()
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "WifiService: Adapter scan failed, falling back to netsh");
                        }
                    }

                    if (list.Count == 0)
                    {
                        list = ScanViaNetsh(connectedSsid, savedProfiles);
                    }

                    // Sort: Connected first, then Saved, then by Signal descending
                    list.Sort((a, b) =>
                    {
                        if (a.IsConnected != b.IsConnected) return b.IsConnected ? 1 : -1;
                        if (a.IsSaved != b.IsSaved) return b.IsSaved ? 1 : -1;
                        return b.SignalPercentage.CompareTo(a.SignalPercentage);
                    });

                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        Networks.Clear();
                        foreach (var item in list)
                        {
                            Networks.Add(item);
                        }
                        NotifyStateChanged();
                    });
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WifiService: Error during RefreshNetworksAsync");
            }
            finally
            {
                IsScanning = false;
                NotifyStateChanged();
            }
        }

        public async Task<bool> ConnectAsync(WifiNetworkItem item)
        {
            if (string.IsNullOrWhiteSpace(item?.Ssid)) return false;
            try
            {
                if (item.IsConnected)
                {
                    await DisconnectAsync();
                    return true;
                }

                if (item.IsSaved)
                {
                    await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo("netsh", $"wlan connect name=\"{item.Ssid}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(3000);
                    });
                    await Task.Delay(1200);
                    await RefreshNetworksAsync();
                    return true;
                }
                else
                {
                    if (_adapter != null)
                    {
                        foreach (var net in _adapter.NetworkReport.AvailableNetworks)
                        {
                            if (net.Ssid == item.Ssid)
                            {
                                var res = await _adapter.ConnectAsync(net, WiFiReconnectionKind.Automatic);
                                if (res.ConnectionStatus == WiFiConnectionStatus.Success)
                                {
                                    await RefreshNetworksAsync();
                                    return true;
                                }
                            }
                        }
                    }
                    // If credentials needed or adapter connect failed, open Windows network flyout
                    Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WifiService: Failed to connect to {Ssid}", item.Ssid);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_adapter != null)
                {
                    _adapter.Disconnect();
                }
                else
                {
                    await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo("netsh", "wlan disconnect")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(2000);
                    });
                }
                await Task.Delay(800);
                await RefreshNetworksAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WifiService: Failed to disconnect");
            }
        }

        private static string GetConnectedSsid()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return string.Empty;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);

                var match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch { }
            return string.Empty;
        }

        private static HashSet<string> GetSavedProfiles()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show profiles")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return set;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);

                var matches = Regex.Matches(output, @"All User Profile\s*:\s*(.+)$", RegexOptions.Multiline);
                foreach (Match m in matches)
                {
                    if (m.Success)
                    {
                        string name = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(name)) set.Add(name);
                    }
                }
            }
            catch { }
            return set;
        }

        private static List<WifiNetworkItem> ScanViaNetsh(string connectedSsid, HashSet<string> savedProfiles)
        {
            var list = new List<WifiNetworkItem>();
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show networks mode=bssid")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return list;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                string[] lines = output.Split('\n');
                WifiNetworkItem? current = null;

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            string ssid = parts[1].Trim();
                            if (!string.IsNullOrEmpty(ssid) && !list.Exists(x => x.Ssid == ssid))
                            {
                                current = new WifiNetworkItem
                                {
                                    Ssid = ssid,
                                    IsConnected = !string.IsNullOrEmpty(connectedSsid) && string.Equals(connectedSsid, ssid, StringComparison.OrdinalIgnoreCase),
                                    IsSaved = savedProfiles.Contains(ssid)
                                };
                                list.Add(current);
                            }
                        }
                    }
                    else if (current != null)
                    {
                        if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length == 2)
                            {
                                string sigStr = parts[1].Replace("%", "").Trim();
                                if (int.TryParse(sigStr, out int sig))
                                {
                                    current.SignalPercentage = sig;
                                    current.SignalBars = sig >= 80 ? 4 : (sig >= 60 ? 3 : (sig >= 40 ? 2 : 1));
                                }
                            }
                        }
                        else if (line.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length == 2)
                            {
                                current.SecurityType = parts[1].Trim();
                                current.IsSecured = !current.SecurityType.Contains("Open", StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WifiService: Netsh scan error");
            }
            return list;
        }
    }
}
