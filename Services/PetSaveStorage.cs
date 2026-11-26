using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PetFrenzy.Models;

namespace PetFrenzy.Services;

/// <summary>
/// Persists PetStateData in an authenticated, AES-256-GCM encrypted binary file (pet.sav)
/// inside the plugin config directory, with automatic migration from legacy plain-text JSON
/// and tamper-resistant backup fallback (pet.sav.bak).
/// </summary>
public static class PetSaveStorage
{
    private static readonly byte[] MagicHeader = Encoding.ASCII.GetBytes("PFS1");
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true, // Required for System.Numerics.Vector4 (X, Y, Z, W fields)
    };

    private static readonly object IoLock = new();

    public static string SaveFilePath =>
        Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "pet.sav");

    private static string BackupFilePath => SaveFilePath + ".bak";

    private static byte[] DeriveKey()
    {
        var material = $"PetFrenzy::v1::n0way02::{Environment.MachineName}::{Environment.UserName}::EorzeaGotchi";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    public static PetStateData LoadOrMigrate(Configuration cfg)
    {
        lock (IoLock)
        {
            // 1. Try primary encrypted save
            if (TryLoadEncryptedFile(SaveFilePath, out var primary))
            {
                WipeLegacyPlaintextIfPresent(cfg);
                return primary;
            }

            // 2. If primary was missing or tampered, try encrypted backup
            if (TryLoadEncryptedFile(BackupFilePath, out var backup))
            {
                Plugin.Log.Warning("[PetFrenzy] Primary pet.sav was missing or tampered; restored from pet.sav.bak.");
                SaveInternal(backup);
                WipeLegacyPlaintextIfPresent(cfg);
                return backup;
            }

            // 3. Migrate legacy plain-text Pet from PetFrenzy.json (one-time seamless migration)
            if (cfg.LegacyPet != null)
            {
                var migrated = cfg.LegacyPet;
                SaveInternal(migrated);
                cfg.LegacyPet = null;
                Plugin.PluginInterface.SavePluginConfig(cfg);
                Plugin.Log.Information("[PetFrenzy] Migrated pet save to AES-256-GCM encrypted pet.sav.");
                return migrated;
            }

            // 4. Fresh companion
            var fresh = new PetStateData();
            SaveInternal(fresh);
            return fresh;
        }
    }

    public static void Save(PetStateData pet)
    {
        lock (IoLock)
        {
            try
            {
                SaveInternal(pet);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "[PetFrenzy] Failed to write encrypted pet.sav");
            }
        }
    }

    private static void WipeLegacyPlaintextIfPresent(Configuration cfg)
    {
        if (cfg.LegacyPet != null)
        {
            cfg.LegacyPet = null;
            Plugin.PluginInterface.SavePluginConfig(cfg);
        }
    }

    private static void SaveInternal(PetStateData pet)
    {
        var path = SaveFilePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(pet, JsonOpts);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var key = DeriveKey();

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, MagicHeader);
        }

        // Format: [4B Magic "PFS1"][12B Nonce][16B GCM Tag][NB Ciphertext]
        var blob = new byte[MagicHeader.Length + NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(MagicHeader, 0, blob, 0, MagicHeader.Length);
        Buffer.BlockCopy(nonce, 0, blob, MagicHeader.Length, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, MagicHeader.Length + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, blob, MagicHeader.Length + NonceSize + TagSize, ciphertext.Length);

        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, blob);

        // Preserve previous valid save as backup before atomic replace
        if (File.Exists(path))
        {
            File.Copy(path, BackupFilePath, overwrite: true);
        }

        File.Move(tmp, path, overwrite: true);
    }

    private static bool TryLoadEncryptedFile(string path, out PetStateData pet)
    {
        pet = null!;
        try
        {
            if (!File.Exists(path)) return false;

            var blob = File.ReadAllBytes(path);
            var minLen = MagicHeader.Length + NonceSize + TagSize + 2;
            if (blob.Length < minLen) return false;

            for (var i = 0; i < MagicHeader.Length; i++)
            {
                if (blob[i] != MagicHeader[i]) return false;
            }

            var nonce = blob.AsSpan(MagicHeader.Length, NonceSize);
            var tag = blob.AsSpan(MagicHeader.Length + NonceSize, TagSize);
            var ciphertext = blob.AsSpan(MagicHeader.Length + NonceSize + TagSize);

            var plaintext = new byte[ciphertext.Length];
            var key = DeriveKey();

            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext, MagicHeader);
            }

            var parsed = JsonSerializer.Deserialize<PetStateData>(plaintext, JsonOpts);
            if (parsed == null) return false;

            pet = parsed;
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            Plugin.Log.Warning($"[PetFrenzy] Integrity check failed for {Path.GetFileName(path)} (tampered or wrong machine).");
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[PetFrenzy] Could not read {Path.GetFileName(path)}.");
            return false;
        }
    }
}
