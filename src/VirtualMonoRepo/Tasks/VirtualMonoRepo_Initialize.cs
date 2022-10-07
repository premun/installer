// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Darc.Models.VirtualMonoRepo;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.DarcLib.VirtualMonoRepo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

/// <summary>
/// This tasks equals calling the "darc vmr initialize" command.
/// This command pulls an individual repository into the VMR for the first time.
/// It can also recursively pull all of its dependencies based on Version.Details.xml.
/// </summary>
public class VirtualMonoRepo_Initialize : Build.Utilities.Task, ICancelableTask
{
    private readonly Lazy<IServiceProvider> _serviceProvider;
    private readonly CancellationTokenSource _cancellationToken = new();

    [Required]
    public string Repository { get; set; }

    [Required]
    public string VmrPath { get; set; }

    [Required]
    public string TmpPath { get; set; }

    public string Revision { get; set; }

    public string PackageVersion { get; set; }

    public bool Recursive { get; set; }

    public VirtualMonoRepo_Initialize()
    {
        _serviceProvider = new(CreateServiceProvider);
    }

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    private async Task<bool> ExecuteAsync()
    {
        var vmrInitializer = _serviceProvider.Value.GetRequiredService<IVmrInitializer>();
        await vmrInitializer.InitializeRepository(Repository, Revision, PackageVersion, Recursive, _cancellationToken.Token);
        return true;
    }

    public void Cancel() => _cancellationToken.Cancel();

    private IServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton<TaskLoggingHelper>(Log);
        serviceCollection.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(MSBuildLogger<>)));
        serviceCollection.Add(ServiceDescriptor.Singleton(typeof(ILogger), typeof(MSBuildLogger<>)));

        serviceCollection.TryAddTransient<IProcessManager>(sp => ActivatorUtilities.CreateInstance<ProcessManager>(sp, "git"));
        serviceCollection.TryAddTransient<ILocalGitRepo>(sp => ActivatorUtilities.CreateInstance<LocalGitClient>(sp, "git"));
        serviceCollection.AddSingleton<IVmrManagerConfiguration>(new VmrManagerConfiguration(VmrPath, TmpPath));
        serviceCollection.TryAddTransient<ISourceMappingParser, SourceMappingParser>();
        serviceCollection.TryAddTransient<IVersionDetailsParser, VersionDetailsParser>();
        serviceCollection.TryAddTransient<IVmrDependencyTracker, VmrDependencyTracker>();
        serviceCollection.TryAddTransient<IVmrPatchHandler, VmrPatchHandler>();
        serviceCollection.TryAddTransient<IVmrUpdater, VmrUpdater>();
        serviceCollection.TryAddTransient<IVmrInitializer, VmrInitializer>();
        serviceCollection.TryAddSingleton<IFileSystem, FileSystem>();
        serviceCollection.TryAddSingleton<IReadOnlyCollection<SourceMapping>>(sp =>
        {
            var configuration = sp.GetRequiredService<IVmrManagerConfiguration>();
            var mappingParser = sp.GetRequiredService<ISourceMappingParser>();
            return mappingParser.ParseMappings(configuration.VmrPath).GetAwaiter().GetResult();
        });

        return serviceCollection.BuildServiceProvider();
    }
}
