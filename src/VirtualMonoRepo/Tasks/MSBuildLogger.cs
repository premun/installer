// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

/// <summary>
/// Adapter between the MSBuild Log and the Microsoft.Extensions ILogger
/// </summary>
class MSBuildLogger<T> : ILogger, ILogger<T>
{
    private readonly TaskLoggingHelper _msbuildLogger;

    public MSBuildLogger(TaskLoggingHelper msbuildLogger)
    {
        _msbuildLogger = msbuildLogger;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            _msbuildLogger.LogMessage(MessageImportance.High, formatter(state, exception));
        }
    }

    private class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
