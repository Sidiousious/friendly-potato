using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MessagePack;
using Timer = System.Timers.Timer;

namespace FriendlyPotato;

public sealed class RuntimeDataManager : IDisposable
{
    private const string FileName = "data.dat";

    private readonly Data data;
    private readonly IPluginLog logger;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ReaderWriterLockSlim readerWriterLock = new(LockRecursionPolicy.NoRecursion);
    private readonly Timer timer = new();
    private volatile bool pendingSave;

    public RuntimeDataManager(IDalamudPluginInterface pi, IPluginLog pl)
    {
        pluginInterface = pi;
        logger = pl;
        if (!Directory.Exists(pi.GetPluginConfigDirectory()))
        {
            Directory.CreateDirectory(pi.GetPluginConfigDirectory());
            data = new Data();
        }
        else
        {
            data = File.Exists(Path.Combine(pi.GetPluginConfigDirectory(), FileName))
                       ? MessagePackSerializer.Deserialize<Data>(
                           File.ReadAllBytes(Path.Combine(pi.GetPluginConfigDirectory(), FileName)))
                       : new Data();
        }

        timer.Elapsed += SaveCallback;
        timer.AutoReset = true;
        timer.Interval = 60_000;
        timer.Start();
    }

    public void Dispose()
    {
        timer.Dispose();
        readerWriterLock.Dispose();
    }

    private void SaveCallback(object? _, ElapsedEventArgs __)
    {
        if (readerWriterLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(10)))
        {
            try
            {
                if (!pendingSave) return;
                logger.Information("Saving runtime data");
                File.WriteAllBytes(Path.Combine(pluginInterface.GetPluginConfigDirectory(), FileName),
                                   MessagePackSerializer.Serialize(data));
                pendingSave = false;
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving data: {ex.Message}");
            } finally
            {
                readerWriterLock.ExitWriteLock();
            }
        }
    }

    public void MarkSeen(string playerName, bool add = true)
    {
        if (readerWriterLock.TryEnterWriteLock(TimeSpan.FromMilliseconds(10)))
        {
            try
            {
                if (!add && !data.Seen.ContainsKey(playerName)) return;
                data.Seen[playerName] = DateTime.Now;
                pendingSave = true;
            } finally
            {
                readerWriterLock.ExitWriteLock();
            }
        }
    }

    public DateTime? LastSeen(string playerName)
    {
        if (readerWriterLock.TryEnterReadLock(TimeSpan.FromMilliseconds(10)))
        {
            try
            {
                return data.Seen.TryGetValue(playerName, out var value) ? value : null;
            } finally
            {
                readerWriterLock.ExitReadLock();
            }
        }

        return null;
    }

    public bool TryGetLastSeen(string playerName, out DateTime lastSeen)
    {
        var dt = LastSeen(playerName);
        if (dt == null)
        {
            lastSeen = DateTime.Now;
            return false;
        }

        lastSeen = dt.Value;
        return true;
    }

    [MessagePackObject]
    public class Data
    {
        [Key(0)]
        public Dictionary<string, DateTime> Seen = new();
    }
}
