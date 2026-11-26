using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using PetFrenzy.Models;

namespace PetFrenzy.Services;

public sealed class RemotePetPeer
{
    public string PlayerKey { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public ushort HomeWorldId { get; init; }
    public ushort CurrentWorldId { get; init; }
    public ushort TerritoryId { get; init; }
    public bool IsParty { get; init; }
    public PetStateData PetData { get; init; } = new();
    public PetMood Mood { get; init; } = PetMood.Idle;
    public JobVisualInfo Job { get; init; } = JobWeaponCatalog.Resolve(0);
    public bool IsWeaponDrawn { get; init; }
    public bool ShowWeapon { get; init; } = true;
    public string SpeechBubble { get; init; } = string.Empty;
}

/// <summary>
/// Lightweight HTTP polling sync client for PetFrenzy Multiplayer.
/// Syncs local pet appearance + live action with n0wayplugins.discloud.app every ~2.5s
/// (or immediately on action/appearance change) and receives peers in the same zone or party.
/// </summary>
public sealed class PetMultiplayerService : IDisposable
{
    private const string SyncEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/sync";
    private const string LeaveEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/leave";
    private const long NormalSyncIntervalMs = 2500;
    private const long FastSyncIntervalMs = 950;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly CancellationTokenSource cts = new();
    private readonly object peerLock = new();

    private Dictionary<string, RemotePetPeer> peersByKey = new(StringComparer.OrdinalIgnoreCase);
    private List<RemotePetPeer> partyPeers = new();
    private int syncInFlight;
    private long lastSyncStartedMs;
    private string lastVisualSignature = string.Empty;
    private string lastLoggedPlayerName = string.Empty;
    private ushort lastLoggedHomeWorldId;
    private bool wasEnabledLastTick;

    public bool IsConnected { get; private set; }
    public int OnlineTotalCount { get; private set; }
    public int ZonePeerCount { get; private set; }
    public int PartyPeerCount { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public PetMultiplayerService()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        SendLeaveFireAndForget();
        cts.Cancel();
        cts.Dispose();
        http.Dispose();
    }

    public bool TryGetPeer(string playerKey, out RemotePetPeer peer)
    {
        lock (peerLock)
        {
            return peersByKey.TryGetValue(playerKey, out peer!);
        }
    }

    public IReadOnlyList<RemotePetPeer> GetPartyPeersSnapshot()
    {
        lock (peerLock)
        {
            return partyPeers;
        }
    }

    public static string MakePlayerKey(string name, uint homeWorldId)
    {
        if (string.IsNullOrWhiteSpace(name) || homeWorldId == 0) return string.Empty;
        return $"{name.Trim().ToLowerInvariant()}@{homeWorldId}";
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        // Trigger a faster sync right after zoning
        lastSyncStartedMs = 0;
    }

    private void OnLogout(int type, int code)
    {
        SendLeaveFireAndForget();
        ClearPeers();
    }

    private void ClearPeers()
    {
        lock (peerLock)
        {
            peersByKey = new Dictionary<string, RemotePetPeer>(StringComparer.OrdinalIgnoreCase);
            partyPeers = new List<RemotePetPeer>();
            ZonePeerCount = 0;
            PartyPeerCount = 0;
            IsConnected = false;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var cfg = Plugin.Config;
        if (!cfg.MultiplayerEnabled)
        {
            if (wasEnabledLastTick)
            {
                wasEnabledLastTick = false;
                SendLeaveFireAndForget();
                ClearPeers();
            }
            return;
        }

        wasEnabledLastTick = true;

        if (!Plugin.ClientState.IsLoggedIn)
            return;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;

        var playerName = local.Name.TextValue;
        var homeWorldId = (ushort)local.HomeWorld.RowId;
        var currentWorldId = (ushort)local.CurrentWorld.RowId;
        var territoryId = Plugin.ClientState.TerritoryType;
        if (string.IsNullOrWhiteSpace(playerName) || homeWorldId == 0 || territoryId == 0)
            return;

        lastLoggedPlayerName = playerName;
        lastLoggedHomeWorldId = homeWorldId;

        var pet = cfg.Pet;
        var sim = Plugin.Sim;
        var sig = $"{territoryId}|{currentWorldId}|{(int)sim.CurrentMood}|{sim.IsWeaponDrawn}|{sim.CurrentJob.JobId}|{(int)pet.Species}|{(int)pet.EquippedAccessory}|{(int)pet.EquippedOutfit}|{(int)pet.EquippedWeaponSkin}|{pet.Level}|{pet.IsHatched}|{pet.IsHibernating}|{cfg.MultiplayerShareOwnPet}";

        var now = Environment.TickCount64;
        var interval = sig != lastVisualSignature ? FastSyncIntervalMs : NormalSyncIntervalMs;
        if (now - lastSyncStartedMs < interval)
            return;

        if (Interlocked.CompareExchange(ref syncInFlight, 1, 0) != 0)
            return;

        lastSyncStartedMs = now;
        lastVisualSignature = sig;

        // Collect party member keys on the main thread (supports BOTH regular PartyList AND Cross-World Party!)
        var partyKeySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var myKey = MakePlayerKey(playerName, homeWorldId);

        foreach (var member in Plugin.PartyList)
        {
            var mKey = MakePlayerKey(member.Name.TextValue, member.World.RowId);
            if (!string.IsNullOrEmpty(mKey) && !string.Equals(mKey, myKey, StringComparison.OrdinalIgnoreCase))
                partyKeySet.Add(mKey);
        }

        // Cross-World Party (when outside a duty with friends on other worlds, PartyList is empty!)
        CollectCrossRealmPartyKeys(myKey, partyKeySet);

        var partyKeys = partyKeySet.ToList();

        var payload = new SyncRequestDto
        {
            PlayerName = playerName,
            HomeWorldId = homeWorldId,
            CurrentWorldId = currentWorldId,
            TerritoryId = (ushort)territoryId,
            ShareOwnPet = cfg.MultiplayerShareOwnPet,
            PartyKeys = partyKeys,
            PetName = pet.Name,
            Species = (int)pet.Species,
            IsHatched = pet.IsHatched,
            EggProgress = pet.EggProgress,
            Level = pet.Level,
            Mood = (int)sim.CurrentMood,
            JobId = sim.CurrentJob.JobId,
            IsWeaponDrawn = sim.IsWeaponDrawn,
            ShowWeapon = cfg.ShowJobWeapon,
            Hat = (int)pet.EquippedAccessory,
            Outfit = (int)pet.EquippedOutfit,
            WeaponSkin = (int)pet.EquippedWeaponSkin,
            UseCustomColors = pet.UseCustomColors,
            BodyColor = pet.UseCustomColors ? [pet.CustomBodyColor.X, pet.CustomBodyColor.Y, pet.CustomBodyColor.Z, 1f] : null,
            AccentColor = pet.UseCustomColors ? [pet.CustomAccentColor.X, pet.CustomAccentColor.Y, pet.CustomAccentColor.Z, 1f] : null,
            DetailColor = pet.UseCustomColors ? [pet.CustomDetailColor.X, pet.CustomDetailColor.Y, pet.CustomDetailColor.Z, 1f] : null,
            IsHibernating = pet.IsHibernating,
            SpeechBubble = sim.SpeechTimerSec > 0.2f ? sim.SpeechBubble : string.Empty,
        };

        var token = cts.Token;
        _ = Task.Run(() => PerformSyncAsync(payload, token), token);
    }

    private async Task PerformSyncAsync(SyncRequestDto payload, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(SyncEndpoint, content, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                IsConnected = false;
                LastError = $"HTTP {(int)resp.StatusCode}";
                return;
            }

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<SyncResponseDto>(body, JsonOpts);
            if (data == null || !data.Ok)
            {
                IsConnected = false;
                return;
            }

            var nextMap = new Dictionary<string, RemotePetPeer>(StringComparer.OrdinalIgnoreCase);
            var nextParty = new List<RemotePetPeer>();

            if (data.Peers != null)
            {
                foreach (var p in data.Peers)
                {
                    if (string.IsNullOrWhiteSpace(p.PlayerKey)) continue;

                    var species = Enum.IsDefined(typeof(PetSpecies), p.Species) ? (PetSpecies)p.Species : PetSpecies.Carbuncle;
                    var mood = Enum.IsDefined(typeof(PetMood), p.Mood) ? (PetMood)p.Mood : PetMood.Idle;
                    var hat = Enum.IsDefined(typeof(PetAccessory), p.Hat) ? (PetAccessory)p.Hat : PetAccessory.None;
                    var outfit = Enum.IsDefined(typeof(PetOutfit), p.Outfit) ? (PetOutfit)p.Outfit : PetOutfit.None;
                    var wSkin = Enum.IsDefined(typeof(WeaponSkin), p.WeaponSkin) ? (WeaponSkin)p.WeaponSkin : WeaponSkin.Default;

                    var petData = new PetStateData
                    {
                        Name = string.IsNullOrWhiteSpace(p.PetName) ? "Buddy" : p.PetName,
                        Species = species,
                        IsHatched = p.IsHatched,
                        EggProgress = p.EggProgress,
                        Level = Math.Clamp(p.Level, 1, PetStateData.MaxLevel),
                        EquippedAccessory = hat,
                        EquippedOutfit = outfit,
                        EquippedWeaponSkin = wSkin,
                        IsHibernating = p.IsHibernating,
                        UseCustomColors = p.UseCustomColors && p.BodyColor is { Length: >= 3 } && p.AccentColor is { Length: >= 3 } && p.DetailColor is { Length: >= 3 },
                        CustomBodyColor = ToVec4(p.BodyColor),
                        CustomAccentColor = ToVec4(p.AccentColor),
                        CustomDetailColor = ToVec4(p.DetailColor),
                    };

                    var peer = new RemotePetPeer
                    {
                        PlayerKey = p.PlayerKey,
                        PlayerName = p.PlayerName,
                        HomeWorldId = p.HomeWorldId,
                        CurrentWorldId = p.CurrentWorldId,
                        TerritoryId = p.TerritoryId,
                        IsParty = p.IsParty,
                        PetData = petData,
                        Mood = mood,
                        Job = JobWeaponCatalog.Resolve((uint)Math.Max(0, p.JobId)),
                        IsWeaponDrawn = p.IsWeaponDrawn,
                        ShowWeapon = p.ShowWeapon,
                        SpeechBubble = p.SpeechBubble ?? string.Empty,
                    };

                    nextMap[p.PlayerKey] = peer;
                    if (p.IsParty)
                        nextParty.Add(peer);
                }
            }

            nextParty.Sort((a, b) => string.Compare(a.PlayerKey, b.PlayerKey, StringComparison.OrdinalIgnoreCase));

            lock (peerLock)
            {
                peersByKey = nextMap;
                partyPeers = nextParty;
                OnlineTotalCount = data.OnlineTotal;
                ZonePeerCount = nextMap.Count;
                PartyPeerCount = nextParty.Count;
                IsConnected = true;
                LastError = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
        }
        finally
        {
            Interlocked.Exchange(ref syncInFlight, 0);
        }
    }

    private void SendLeaveFireAndForget()
    {
        var name = lastLoggedPlayerName;
        var hw = lastLoggedHomeWorldId;
        if (string.IsNullOrWhiteSpace(name) || hw == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { playerName = name, homeWorldId = hw });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(LeaveEndpoint, content).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        });
    }

    private static unsafe void CollectCrossRealmPartyKeys(string myKey, HashSet<string> partyKeySet)
    {
        var cr = InfoProxyCrossRealm.Instance();
        if (cr == null || !cr->IsCrossRealm) return;

        var groupIdx = cr->LocalPlayerGroupIndex;
        if (groupIdx >= cr->CrossRealmGroups.Length) return;

        var grp = cr->CrossRealmGroups[groupIdx];
        var count = Math.Min((int)grp.GroupMemberCount, grp.GroupMembers.Length);
        for (var i = 0; i < count; i++)
        {
            var m = grp.GroupMembers[i];
            var mKey = MakePlayerKey(m.NameString, (uint)m.HomeWorld);
            if (!string.IsNullOrEmpty(mKey) && !string.Equals(mKey, myKey, StringComparison.OrdinalIgnoreCase))
                partyKeySet.Add(mKey);
        }
    }

    private static Vector4 ToVec4(float[]? arr)
        => arr is { Length: >= 3 } ? new Vector4(arr[0], arr[1], arr[2], arr.Length >= 4 ? arr[3] : 1f) : Vector4.One;

    private sealed class SyncRequestDto
    {
        public string PlayerName { get; set; } = string.Empty;
        public ushort HomeWorldId { get; set; }
        public ushort CurrentWorldId { get; set; }
        public ushort TerritoryId { get; set; }
        public bool ShareOwnPet { get; set; }
        public List<string> PartyKeys { get; set; } = new();
        public string PetName { get; set; } = string.Empty;
        public int Species { get; set; }
        public bool IsHatched { get; set; }
        public float EggProgress { get; set; }
        public int Level { get; set; }
        public int Mood { get; set; }
        public uint JobId { get; set; }
        public bool IsWeaponDrawn { get; set; }
        public bool ShowWeapon { get; set; }
        public int Hat { get; set; }
        public int Outfit { get; set; }
        public int WeaponSkin { get; set; }
        public bool UseCustomColors { get; set; }
        public float[]? BodyColor { get; set; }
        public float[]? AccentColor { get; set; }
        public float[]? DetailColor { get; set; }
        public bool IsHibernating { get; set; }
        public string SpeechBubble { get; set; } = string.Empty;
    }

    private sealed class SyncResponseDto
    {
        public bool Ok { get; set; }
        public long ServerTime { get; set; }
        public int OnlineTotal { get; set; }
        public List<PeerDto>? Peers { get; set; }
    }

    private sealed class PeerDto
    {
        public string PlayerKey { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public ushort HomeWorldId { get; set; }
        public ushort CurrentWorldId { get; set; }
        public ushort TerritoryId { get; set; }
        public bool IsParty { get; set; }
        public string PetName { get; set; } = string.Empty;
        public int Species { get; set; }
        public bool IsHatched { get; set; }
        public float EggProgress { get; set; }
        public int Level { get; set; }
        public int Mood { get; set; }
        public int JobId { get; set; }
        public bool IsWeaponDrawn { get; set; }
        public bool ShowWeapon { get; set; } = true;
        public int Hat { get; set; }
        public int Outfit { get; set; }
        public int WeaponSkin { get; set; }
        public bool UseCustomColors { get; set; }
        public float[]? BodyColor { get; set; }
        public float[]? AccentColor { get; set; }
        public float[]? DetailColor { get; set; }
        public bool IsHibernating { get; set; }
        public string? SpeechBubble { get; set; }
    }
}
