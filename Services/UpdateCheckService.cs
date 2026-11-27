using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PetFrenzy.Services;

/// <summary>
/// Periodically checks PetFrenzy's GitHub pluginmaster.json (every 30 min) so your companion can
/// notify you via a cute speech bubble whenever a newer PetFrenzy version is released!
/// </summary>
public sealed class UpdateCheckService : IDisposable
{
    private const string ManifestUrl = "https://n0wayplugins.discloud.app/repo.json";
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly CancellationTokenSource cts = new();

    public string? AvailableVersion { get; private set; }
    public bool IsDismissed { get; private set; }

    public bool HasVisibleUpdateNotice => !string.IsNullOrEmpty(AvailableVersion) && !IsDismissed;

    public UpdateCheckService()
    {
        _ = Task.Run(() => LoopAsync(cts.Token), cts.Token);
    }

    public void Dismiss()
    {
        IsDismissed = true;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InitialDelay, ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                await CheckOnceAsync(ct).ConfigureAwait(false);
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch
        {
            // Best-effort background check
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(ManifestUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<ManifestEntryDto>>(body, JsonOpts);
            var petEntry = entries?.Find(e => string.Equals(e.InternalName, "PetFrenzy", StringComparison.OrdinalIgnoreCase));
            var remoteStr = petEntry?.AssemblyVersion;
            if (string.IsNullOrWhiteSpace(remoteStr)) return;

            if (Version.TryParse(remoteStr, out var remoteVer)
                && Version.TryParse(Plugin.CurrentVersion, out var localVer)
                && remoteVer > localVer)
            {
                if (!string.Equals(AvailableVersion, remoteStr, StringComparison.Ordinal))
                {
                    AvailableVersion = remoteStr;
                    IsDismissed = false;
                }
            }
        }
        catch
        {
            // Silent fail if offline / rate-limited
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        http.Dispose();
        cts.Dispose();
    }

    private sealed class ManifestEntryDto
    {
        public string? InternalName { get; set; }
        public string? AssemblyVersion { get; set; }
    }
}
