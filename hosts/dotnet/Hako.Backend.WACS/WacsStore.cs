using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using Wacs.Core.Runtime;
using Wacs.Core.Runtime.Types;
using Wacs.Core.Types;
using Wacs.WASIp1;

namespace HakoJS.Backend.Wacs;

public sealed class WacsStore : WasmStore
{
    private readonly WasmStoreOptions _options;
    internal readonly WasmRuntime Runtime;
    internal readonly Wasi? Wasi;
    private bool _disposed;

    internal WacsStore(WacsEngineOptions engineOptions, WasmStoreOptions? storeOptions)
    {
        _options = storeOptions ?? new WasmStoreOptions
        {
            WasiConfiguration = storeOptions?.WasiConfiguration,
            MaxMemoryBytes = storeOptions?.MaxMemoryBytes
        };

        Runtime = new WasmRuntime
        {
            TranspileModules = engineOptions.TranspileModules
        };

        if (_options.WasiConfiguration != null)
        {
            var wasiConfig = ConvertWasiConfiguration(_options.WasiConfiguration);
            Wasi = new Wasi(wasiConfig);
        }
    }

    public override WasmLinker CreateLinker()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WacsLinker(this, _options);
    }


    public override WasmMemory CreateMemory(MemoryConfiguration config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(config);

        var memoryType = new MemoryType(config.InitialPages, config.MaximumPages);
        var memoryInstance = new MemoryInstance(memoryType);
        return new WacsMemory(memoryInstance);
    }

    internal WasmMemory CreateMemory(uint initialPages, uint? maximumPages)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var memType = new MemoryType(initialPages, maximumPages);
        var memoryInstance = Runtime.BindHostMemory(("env", "memory"), memType);
        return new WacsMemory(memoryInstance);
    }

    private static WasiConfiguration ConvertWasiConfiguration(HakoWasiConfiguration config)
    {
        List<string> args;
        if (config.Arguments != null)
            args = config.Arguments.ToList();
        else if (config.InheritEnvironment)
            args = Environment.GetCommandLineArgs().Skip(1).ToList();
        else
            args = [];

        Dictionary<string, string> envVars;
        if (config.EnvironmentVariables != null)
            envVars = config.EnvironmentVariables;
        else if (config.InheritEnvironment)
            envVars = GetHostEnvironmentVariables();
        else
            envVars = [];

        return new WasiConfiguration
        {
            StandardInput = config.InheritStdio ? Console.OpenStandardInput() : Stream.Null,
            StandardOutput = config.InheritStdio ? Console.OpenStandardOutput() : Stream.Null,
            StandardError = config.InheritStdio ? Console.OpenStandardError() : Stream.Null,
            Arguments = args,
            EnvironmentVariables = envVars,
            HostRootDirectory = config.PreopenDirectory ?? Directory.GetCurrentDirectory()
        };
    }

    private static Dictionary<string, string> GetHostEnvironmentVariables()
    {
        return Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "");
    }


    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing) Wasi?.Dispose();

        _disposed = true;
        base.Dispose(disposing);
    }
}