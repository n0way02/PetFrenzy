using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
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

public sealed class BossContributorInfo
{
    public string PlayerName { get; init; } = string.Empty;
    public string PetName { get; init; } = string.Empty;
    public int Damage { get; init; }
}

public sealed class PartyBossSnapshot
{
    public string BossId { get; init; } = string.Empty;
    public int BossType { get; init; }
    public int BossLevel { get; init; } = 20;
    public string BossName { get; init; } = string.Empty;
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int PartyHp { get; init; }
    public int PartyMaxHp { get; init; }
    public int LbGauge { get; init; }
    public bool ShieldActive { get; init; }
    public string BossCastName { get; init; } = string.Empty;
    public float BossCastProgress { get; init; }
    public bool Defeated { get; init; }
    public bool Wiped { get; init; }
    public bool IsEnded => Defeated || Wiped;
    public int RewardCoins { get; init; }
    public int RewardXp { get; init; }
    public int RemainingSec { get; init; }
    public string SummonedBy { get; init; } = string.Empty;
    public string LastHitText { get; init; } = string.Empty;
    public long LastHitAt { get; init; }
    public List<string> ParticipantKeys { get; init; } = new();
    public List<BossContributorInfo> Contributors { get; init; } = new();
}

public sealed class BossInviteSnapshot
{
    public string InviteId { get; init; } = string.Empty;
    public string RoomId { get; init; } = string.Empty;
    public string BossId { get; init; } = string.Empty;
    public string BossName { get; init; } = string.Empty;
    public int BossLevel { get; init; } = 20;
    public string FromPlayerName { get; init; } = string.Empty;
    public string FromPetName { get; init; } = string.Empty;
    public int RemainingSec { get; init; }
}

/// <summary>
/// Lightweight HTTP polling sync client for PetFrenzy Multiplayer.
/// Handles zone/party pet presence, live petting & gift inbox, and Party Co-op Mini-Bosses.
/// </summary>
public sealed class PetMultiplayerService : IDisposable
{
    private const string SyncEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/sync";
    private const string LeaveEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/leave";
    private const string InteractEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/interact";
    private const string BossStartEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/boss/start";
    private const string BossAttackEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/boss/attack";
    private const string BossInviteZoneEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/boss/invite-zone";
    private const string BossRespondEndpoint = "https://n0wayplugins.discloud.app/api/petfrenzy/boss/respond";

    private const long NormalSyncIntervalMs = 4000;
    private const long FastSyncIntervalMs = 950;
    private const long BossActiveSyncIntervalMs = 1350;

    public const int GiftSnackCost = 40;
    public const int GiftPotionCost = 35;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly CancellationTokenSource cts = new();
    private readonly object peerLock = new();
    private readonly ConcurrentQueue<InboxEventDto> pendingInbox = new();
    private readonly ConcurrentQueue<(string BossName, int Coins, int Xp)> pendingBossRewards = new();
    private readonly ConcurrentQueue<(int Dmg, bool IsCrit)> pendingAttackReactions = new();
    private readonly HashSet<string> rewardedBossIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> localInteractCooldowns = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, RemotePetPeer> peersByKey = new(StringComparer.OrdinalIgnoreCase);
    private List<RemotePetPeer> partyPeers = new();
    private List<RemotePetPeer> allPeersSorted = new();
    private List<string> cachedPartyKeys = new();
    private PartyBossSnapshot? activeBoss;
    private BossInviteSnapshot? pendingBossInvite;
    private string lastDismissedInviteId = string.Empty;
    private int syncInFlight;
    private int bossActionInFlight;
    private long lastSyncStartedMs;
    private long lastAutoAttackMs;
    private long lastBurstSkillMs;
    private long lastUltSkillMs;
    private long lastLbSkillMs;
    private long lastHealSkillMs;
    private long lastSeenBossHitAt;
    private string lastVisualSignature = string.Empty;
    private string lastLoggedPlayerName = string.Empty;
    private ushort lastLoggedHomeWorldId;
    private bool wasEnabledLastTick;

    public bool IsConnected { get; private set; }
    public int OnlineTotalCount { get; private set; }
    public int ZonePeerCount { get; private set; }
    public int PartyPeerCount { get; private set; }
    public BossInviteSnapshot? PendingBossInvite { get { lock (peerLock) return pendingBossInvite; } }

    public List<RemotePetPeer> GetBossFighterPeers(PartyBossSnapshot? boss)
    {
        lock (peerLock)
        {
            if (boss == null) return new List<RemotePetPeer>(partyPeers);
            var result = new List<RemotePetPeer>(7);
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in partyPeers)
            {
                if (added.Add(p.PlayerKey)) result.Add(p);
            }
            foreach (var pk in boss.ParticipantKeys)
            {
                if (string.IsNullOrWhiteSpace(pk) || added.Contains(pk)) continue;
                if (peersByKey.TryGetValue(pk, out var zonePeer) && added.Add(zonePeer.PlayerKey))
                    result.Add(zonePeer);
            }
            return result;
        }
    }
    public string LastError { get; private set; } = string.Empty;
    public string SocialToast { get; private set; } = string.Empty;
    public float SocialToastTimerSec { get; private set; }
    public float BossHitShakeSec { get; private set; }
    public float LocalLungeTimerSec { get; private set; }
    public bool ShowBossBattleHud { get; set; } = true;
    public bool BossHudDismissedForCurrentId { get; private set; }

    public PartyBossSnapshot? ActiveBoss
    {
        get { lock (peerLock) return activeBoss; }
    }

    public float BurstCooldownSec => MathF.Max(0f, (7500L - (Environment.TickCount64 - lastBurstSkillMs)) / 1000f);
    public float UltimateCooldownSec => MathF.Max(0f, (12500L - (Environment.TickCount64 - lastUltSkillMs)) / 1000f);
    public float LimitBreakCooldownSec => MathF.Max(0f, (18500L - (Environment.TickCount64 - lastLbSkillMs)) / 1000f);
    public float HealCooldownSec => MathF.Max(0f, (8500L - (Environment.TickCount64 - lastHealSkillMs)) / 1000f);
    public float AttackCooldownRemainingSec => MathF.Max(0f, (1500L - (Environment.TickCount64 - lastAutoAttackMs)) / 1000f);

    public void DismissBossHud() => BossHudDismissedForCurrentId = true;

    public static string GetSpeciesUltimateName(PetSpecies sp) => sp switch
    {
        PetSpecies.Carbuncle   => L.T("Ruby Searing Light", "Luz Rubi"),
        PetSpecies.FatCat      => L.T("Mega Belly Slam", "Barrigada Mega"),
        PetSpecies.Moogle      => L.T("Pom Flare", "Pom Flare"),
        PetSpecies.Namazu      => L.T("Wasshoi Rush", "Fúria Wasshoi"),
        PetSpecies.FluffyKitty => L.T("Milly's Void Pounce", "Salto da Milly"),
        PetSpecies.ShepherdDog => L.T("Alpha Fang Blitz", "Mordida Alfa"),
        PetSpecies.Deviruchi   => L.T("Dark Trident Pierce", "Tridente Sombrio"),
        _                      => L.T("Pet Nova", "Nova do Pet"),
    };

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
        lock (peerLock) return partyPeers;
    }

    public IReadOnlyList<RemotePetPeer> GetAllPeersSnapshot()
    {
        lock (peerLock) return allPeersSorted;
    }

    public static string MakePlayerKey(string name, uint homeWorldId)
    {
        if (string.IsNullOrWhiteSpace(name) || homeWorldId == 0) return string.Empty;
        return $"{name.Trim().ToLowerInvariant()}@{homeWorldId}";
    }

    public float GetInteractCooldownSec(string targetKey, string kind)
    {
        var cdKey = $"{targetKey}:{kind}";
        var cdMs = kind == "pet" ? 12_000L : 3_000L;
        if (!localInteractCooldowns.TryGetValue(cdKey, out var lastAt)) return 0f;
        return MathF.Max(0f, (cdMs - (Environment.TickCount64 - lastAt)) / 1000f);
    }

    /// <summary>
    /// Send a live headpat ("pet"), Kupo Nut Snack ("snack"), or Aether Espresso ("potion") to another player's pet!
    /// </summary>
    public bool SendInteraction(RemotePetPeer target, string kind)
    {
        if (!Plugin.Config.MultiplayerEnabled || !Plugin.ClientState.IsLoggedIn) return false;
        if (string.IsNullOrWhiteSpace(lastLoggedPlayerName) || lastLoggedHomeWorldId == 0) return false;

        if (GetInteractCooldownSec(target.PlayerKey, kind) > 0f)
            return false;

        var cost = kind switch
        {
            "snack" => GiftSnackCost,
            "potion" => GiftPotionCost,
            _ => 0,
        };

        var giftLabel = kind switch
        {
            "snack" => L.T("Kupo Nut Snack", "Noz Kupo"),
            "potion" => L.T("Aether Espresso", "Expresso Etéreo"),
            _ => L.T("Headpat", "Carinho"),
        };

        if (cost > 0 && !Plugin.Sim.TrySpendCoinsForGift(cost, target.PetData.Name, giftLabel))
            return false;

        localInteractCooldowns[$"{target.PlayerKey}:{kind}"] = Environment.TickCount64;
        ShowToast(kind == "pet"
            ? L.T($"♥ You petted {target.PetData.Name} ({target.PlayerName})!", $"♥ Você fez carinho em {target.PetData.Name} ({target.PlayerName})!")
            : L.T($"★ Sent {giftLabel} to {target.PetData.Name}!", $"★ Enviou {giftLabel} para {target.PetData.Name}!"));

        var req = new
        {
            fromPlayerName = lastLoggedPlayerName,
            fromHomeWorldId = lastLoggedHomeWorldId,
            fromPetName = Plugin.Config.Pet.Name,
            targetPlayerKey = target.PlayerKey,
            kind,
        };

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(req, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(InteractEndpoint, content, token).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }, token);

        return true;
    }

    public void SummonPartyBoss(int bossType, bool inviteZone = false)
    {
        if (!Plugin.Config.MultiplayerEnabled || !Plugin.ClientState.IsLoggedIn) return;
        if (string.IsNullOrWhiteSpace(lastLoggedPlayerName) || lastLoggedHomeWorldId == 0) return;
        if (Interlocked.CompareExchange(ref bossActionInFlight, 1, 0) != 0) return;

        BossHudDismissedForCurrentId = false;
        ShowBossBattleHud = true;
        var partyCopy = cachedPartyKeys.ToList();
        var req = new
        {
            playerName = lastLoggedPlayerName,
            homeWorldId = lastLoggedHomeWorldId,
            petLevel = Plugin.Config.Pet.Level,
            partyKeys = partyCopy,
            bossType,
            inviteZone,
        };

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(req, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(BossStartEndpoint, content, token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;
                var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<BossActionResponseDto>(body, JsonOpts);
                if (data?.ActiveBoss != null)
                    ApplyBossUpdate(data.ActiveBoss);
                if (inviteZone && data != null && data.InvitedCount > 0)
                    ShowToast(L.T($"Invited {data.InvitedCount} nearby player(s) to the Mini-Boss!", $"Convite enviado para {data.InvitedCount} jogador(es) da área!"));
            }
            catch { /* ignore */ }
            finally
            {
                Interlocked.Exchange(ref bossActionInFlight, 0);
            }
        }, token);
    }

    public void InviteZoneToActiveBoss()
    {
        if (!Plugin.Config.MultiplayerEnabled || !Plugin.ClientState.IsLoggedIn) return;
        if (string.IsNullOrWhiteSpace(lastLoggedPlayerName) || lastLoggedHomeWorldId == 0) return;
        if (ActiveBoss is not { IsEnded: false }) return;

        var partyCopy = cachedPartyKeys.ToList();
        var req = new
        {
            playerName = lastLoggedPlayerName,
            homeWorldId = lastLoggedHomeWorldId,
            partyKeys = partyCopy,
        };

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(req, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(BossInviteZoneEndpoint, content, token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;
                var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<BossActionResponseDto>(body, JsonOpts);
                if (data?.ActiveBoss != null)
                    ApplyBossUpdate(data.ActiveBoss);
                ShowToast(data != null && data.InvitedCount > 0
                    ? L.T($"Invited {data.InvitedCount} nearby player(s)!", $"Convite enviado para {data.InvitedCount} jogador(es) da área!")
                    : L.T("No other PetFrenzy players available in this zone.", "Nenhum outro jogador com PetFrenzy disponível na área."));
            }
            catch { /* ignore */ }
        }, token);
    }

    public void RespondToBossInvite(bool accept)
    {
        BossInviteSnapshot? inv;
        lock (peerLock)
        {
            inv = pendingBossInvite;
            pendingBossInvite = null;
            if (inv != null) lastDismissedInviteId = inv.InviteId;
        }
        if (inv == null) return;
        if (!Plugin.Config.MultiplayerEnabled || !Plugin.ClientState.IsLoggedIn) return;
        if (string.IsNullOrWhiteSpace(lastLoggedPlayerName) || lastLoggedHomeWorldId == 0) return;

        if (accept)
        {
            BossHudDismissedForCurrentId = false;
            ShowBossBattleHud = true;
        }

        var req = new
        {
            playerName = lastLoggedPlayerName,
            homeWorldId = lastLoggedHomeWorldId,
            petName = Plugin.Config.Pet.Name,
            petLevel = Plugin.Config.Pet.Level,
            inviteId = inv.InviteId,
            accept,
        };

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(req, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(BossRespondEndpoint, content, token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;
                var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<BossActionResponseDto>(body, JsonOpts);
                if (accept && data?.ActiveBoss != null)
                    ApplyBossUpdate(data.ActiveBoss);
            }
            catch { /* ignore */ }
        }, token);
    }

    public void AttackPartyBoss(string skillKind = "auto", string skillName = "")
    {
        if (!Plugin.Config.MultiplayerEnabled || !Plugin.ClientState.IsLoggedIn) return;
        if (string.IsNullOrWhiteSpace(lastLoggedPlayerName) || lastLoggedHomeWorldId == 0) return;

        var now = Environment.TickCount64;
        switch (skillKind)
        {
            case "burst":
                if (BurstCooldownSec > 0f) return;
                lastBurstSkillMs = now;
                break;
            case "ultimate":
                if (UltimateCooldownSec > 0f) return;
                lastUltSkillMs = now;
                break;
            case "lb":
                if (ActiveBoss is not { LbGauge: >= 100 }) return;
                lastLbSkillMs = now;
                break;
            case "heal":
                if (HealCooldownSec > 0f) return;
                lastHealSkillMs = now;
                break;
            default:
                if (AttackCooldownRemainingSec > 0f) return;
                lastAutoAttackMs = now;
                break;
        }

        if (Interlocked.CompareExchange(ref bossActionInFlight, 1, 0) != 0) return;
        LocalLungeTimerSec = 0.35f;

        var partyCopy = cachedPartyKeys.ToList();
        var req = new
        {
            playerName = lastLoggedPlayerName,
            homeWorldId = lastLoggedHomeWorldId,
            petName = Plugin.Config.Pet.Name,
            petLevel = Plugin.Config.Pet.Level,
            skillKind,
            skillName,
            partyKeys = partyCopy,
        };

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(req, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(BossAttackEndpoint, content, token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return;
                var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<BossActionResponseDto>(body, JsonOpts);
                if (data != null && data.Ok)
                {
                    if (data.Damage > 0 && skillKind != "auto")
                        pendingAttackReactions.Enqueue((data.Damage, data.IsCrit));
                    if (data.ActiveBoss != null)
                        ApplyBossUpdate(data.ActiveBoss);
                }
            }
            catch { /* ignore */ }
            finally
            {
                Interlocked.Exchange(ref bossActionInFlight, 0);
            }
        }, token);
    }

    private void ShowToast(string msg)
    {
        SocialToast = msg;
        SocialToastTimerSec = 4.0f;
    }

    private void OnTerritoryChanged(uint territoryId) => lastSyncStartedMs = 0;

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
            allPeersSorted = new List<RemotePetPeer>();
            activeBoss = null;
            ZonePeerCount = 0;
            PartyPeerCount = 0;
            IsConnected = false;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var dt = (float)framework.UpdateDelta.TotalSeconds;
        if (SocialToastTimerSec > 0f) SocialToastTimerSec = MathF.Max(0f, SocialToastTimerSec - dt);
        if (BossHitShakeSec > 0f) BossHitShakeSec = MathF.Max(0f, BossHitShakeSec - dt);
        if (LocalLungeTimerSec > 0f) LocalLungeTimerSec = MathF.Max(0f, LocalLungeTimerSec - dt);

        // Automatic Pet Auto-Attack every ~1.8s while a Mini-Boss is alive!
        var curBoss = ActiveBoss;
        if (curBoss is { IsEnded: false } && !Plugin.Config.Pet.IsHibernating && Environment.TickCount64 - lastAutoAttackMs >= 1800)
        {
            AttackPartyBoss("auto", "");
        }

        // Drain queued social interactions & boss rewards safely on the main framework thread!
        while (pendingInbox.TryDequeue(out var ev))
        {
            Plugin.Sim.ReceiveSocialInteraction(ev.FromPlayerName, ev.FromPetName, ev.Kind);
        }
        while (pendingAttackReactions.TryDequeue(out var atk))
        {
            Plugin.Sim.TriggerBossAttackReaction(atk.Dmg, atk.IsCrit);
        }
        while (pendingBossRewards.TryDequeue(out var rew))
        {
            Plugin.Sim.GrantPartyBossReward(rew.BossName, rew.Coins, rew.Xp);
        }

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

        var hasActiveBoss = ActiveBoss is { IsEnded: false };
        var now = Environment.TickCount64;
        var interval = sig != lastVisualSignature
            ? FastSyncIntervalMs
            : (hasActiveBoss ? BossActiveSyncIntervalMs : NormalSyncIntervalMs);

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

        CollectCrossRealmPartyKeys(myKey, partyKeySet);

        var partyKeys = partyKeySet.ToList();
        cachedPartyKeys = partyKeys;

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

            if (data.Inbox != null)
            {
                foreach (var ev in data.Inbox)
                    pendingInbox.Enqueue(ev);
            }

            ApplyBossUpdate(data.ActiveBoss);

            lock (peerLock)
            {
                if (data.BossInvite != null
                    && !string.IsNullOrEmpty(data.BossInvite.InviteId)
                    && !string.Equals(data.BossInvite.InviteId, lastDismissedInviteId, StringComparison.Ordinal))
                {
                    pendingBossInvite = new BossInviteSnapshot
                    {
                        InviteId = data.BossInvite.InviteId,
                        RoomId = data.BossInvite.RoomId,
                        BossId = data.BossInvite.BossId,
                        BossName = data.BossInvite.BossName ?? "Mini-Boss",
                        BossLevel = data.BossInvite.BossLevel > 0 ? data.BossInvite.BossLevel : 20,
                        FromPlayerName = data.BossInvite.FromPlayerName ?? "Player",
                        FromPetName = data.BossInvite.FromPetName ?? "Buddy",
                        RemainingSec = data.BossInvite.RemainingSec,
                    };
                }
                else
                {
                    pendingBossInvite = null;
                }
            }

            var nextMap = new Dictionary<string, RemotePetPeer>(StringComparer.OrdinalIgnoreCase);
            var nextParty = new List<RemotePetPeer>();
            var nextAll = new List<RemotePetPeer>();

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
                    nextAll.Add(peer);
                    if (p.IsParty)
                        nextParty.Add(peer);
                }
            }

            nextParty.Sort((a, b) => string.Compare(a.PlayerKey, b.PlayerKey, StringComparison.OrdinalIgnoreCase));
            nextAll.Sort((a, b) =>
            {
                var cmp = b.IsParty.CompareTo(a.IsParty);
                return cmp != 0 ? cmp : string.Compare(a.PlayerName, b.PlayerName, StringComparison.OrdinalIgnoreCase);
            });

            lock (peerLock)
            {
                peersByKey = nextMap;
                partyPeers = nextParty;
                allPeersSorted = nextAll;
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

    private void ApplyBossUpdate(BossDto? dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.BossId))
        {
            lock (peerLock) activeBoss = null;
            return;
        }

        if (dto.LastHitAt > lastSeenBossHitAt)
        {
            lastSeenBossHitAt = dto.LastHitAt;
            BossHitShakeSec = 0.28f;
        }

        var snap = new PartyBossSnapshot
        {
            BossId = dto.BossId,
            BossType = dto.BossType,
            BossLevel = dto.BossLevel > 0 ? dto.BossLevel : 20,
            BossName = dto.BossName ?? "Mini-Boss",
            Hp = dto.Hp,
            MaxHp = Math.Max(1, dto.MaxHp),
            PartyHp = dto.PartyHp,
            PartyMaxHp = dto.PartyMaxHp,
            LbGauge = Math.Clamp(dto.LbGauge, 0, 100),
            ShieldActive = dto.ShieldActive,
            BossCastName = dto.BossCastName ?? string.Empty,
            BossCastProgress = dto.BossCastProgress,
            Defeated = dto.Defeated,
            Wiped = dto.Wiped,
            RewardCoins = dto.RewardCoins,
            RewardXp = dto.RewardXp,
            RemainingSec = dto.RemainingSec,
            SummonedBy = dto.SummonedBy ?? string.Empty,
            LastHitText = dto.LastHitText ?? string.Empty,
            LastHitAt = dto.LastHitAt,
            ParticipantKeys = dto.ParticipantKeys ?? new List<string>(),
            Contributors = dto.Contributors?.Select(c => new BossContributorInfo
            {
                PlayerName = c.PlayerName ?? string.Empty,
                PetName = c.PetName ?? string.Empty,
                Damage = c.Damage,
            }).ToList() ?? new List<BossContributorInfo>(),
        };

        lock (peerLock)
        {
            if (activeBoss == null || !string.Equals(activeBoss.BossId, snap.BossId, StringComparison.Ordinal))
                BossHudDismissedForCurrentId = false;
            activeBoss = snap;
            if (snap.Defeated && !rewardedBossIds.Contains(snap.BossId))
            {
                var contributed = snap.Contributors.Any(c =>
                    string.Equals(c.PlayerName, lastLoggedPlayerName, StringComparison.OrdinalIgnoreCase));
                if (contributed)
                {
                    rewardedBossIds.Add(snap.BossId);
                    if (rewardedBossIds.Count > 50) rewardedBossIds.Clear();
                    pendingBossRewards.Enqueue((snap.BossName, snap.RewardCoins, snap.RewardXp));
                }
            }
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
        public List<InboxEventDto>? Inbox { get; set; }
        public BossDto? ActiveBoss { get; set; }
        public BossInviteDto? BossInvite { get; set; }
    }

    private sealed class BossInviteDto
    {
        public string InviteId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string BossId { get; set; } = string.Empty;
        public string? BossName { get; set; }
        public int BossLevel { get; set; }
        public string? FromPlayerName { get; set; }
        public string? FromPetName { get; set; }
        public int RemainingSec { get; set; }
    }

    private sealed class InboxEventDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "pet";
        public string FromPlayerName { get; set; } = string.Empty;
        public string FromPetName { get; set; } = string.Empty;
        public long At { get; set; }
    }

    private sealed class BossActionResponseDto
    {
        public bool Ok { get; set; }
        public int Damage { get; set; }
        public bool IsCrit { get; set; }
        public int InvitedCount { get; set; }
        public BossDto? ActiveBoss { get; set; }
    }

    private sealed class BossDto
    {
        public string BossId { get; set; } = string.Empty;
        public int BossType { get; set; }
        public int BossLevel { get; set; }
        public string? BossName { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int PartyHp { get; set; }
        public int PartyMaxHp { get; set; }
        public int LbGauge { get; set; }
        public bool ShieldActive { get; set; }
        public string? BossCastName { get; set; }
        public float BossCastProgress { get; set; }
        public bool Defeated { get; set; }
        public bool Wiped { get; set; }
        public int RewardCoins { get; set; }
        public int RewardXp { get; set; }
        public int RemainingSec { get; set; }
        public string? SummonedBy { get; set; }
        public string? LastHitText { get; set; }
        public long LastHitAt { get; set; }
        public List<string>? ParticipantKeys { get; set; }
        public List<BossContributorDto>? Contributors { get; set; }
    }

    private sealed class BossContributorDto
    {
        public string? PlayerName { get; set; }
        public string? PetName { get; set; }
        public int Damage { get; set; }
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
