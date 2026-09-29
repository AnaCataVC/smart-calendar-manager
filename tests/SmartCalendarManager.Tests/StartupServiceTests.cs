using System;
using System.Collections.Generic;
using System.Security;
using Microsoft.Extensions.Logging;
using Moq;
using SmartCalendarManager.Core.Common;
using SmartCalendarManager.Core.Services;
using Xunit;

namespace SmartCalendarManager.Tests;

/// <summary>
/// In-memory test double for <see cref="IStartupRegistrationProvider"/> simulating
/// OS startup autolaunch entries without touching the Windows Registry.
/// </summary>
public class InMemoryStartupRegistrationProvider : IStartupRegistrationProvider
{
    private readonly Dictionary<string, string> _registrations = new(StringComparer.OrdinalIgnoreCase);

    public bool ThrowOnRegister { get; set; }
    public Exception? RegisterException { get; set; }

    public bool ThrowOnUnregister { get; set; }
    public Exception? UnregisterException { get; set; }

    public bool IsRegistered(string appName)
    {
        return _registrations.ContainsKey(appName);
    }

    public string? GetRegisteredCommand(string appName)
    {
        return _registrations.TryGetValue(appName, out var command) ? command : null;
    }

    public void Register(string appName, string commandLine)
    {
        if (ThrowOnRegister)
        {
            throw RegisterException ?? new UnauthorizedAccessException("Simulated registry write access denied.");
        }

        _registrations[appName] = commandLine;
    }

    public void Unregister(string appName)
    {
        if (ThrowOnUnregister)
        {
            throw UnregisterException ?? new UnauthorizedAccessException("Simulated registry delete access denied.");
        }

        _registrations.Remove(appName);
    }

    public void SeedRegistration(string appName, string commandLine)
    {
        _registrations[appName] = commandLine;
    }

    public void Clear()
    {
        _registrations.Clear();
    }
}

public class StartupServiceTests
{
    private readonly InMemoryStartupRegistrationProvider _provider;
    private readonly Mock<ILogger<StartupService>> _loggerMock;
    private readonly StartupService _sut;

    public StartupServiceTests()
    {
        _provider = new InMemoryStartupRegistrationProvider();
        _loggerMock = new Mock<ILogger<StartupService>>();
        _sut = new StartupService(_provider, _loggerMock.Object);
    }

    #region Scenario 1: Enable startup with default minimized flag

    [Fact]
    public void SetStartup_EnableWithDefaultMinimized_RegistersCommandWithMinimizedFlagAndSetsProperties()
    {
        // Arrange
        var appName = _sut.AppName;
        var exePath = _sut.GetCurrentExecutablePath();

        // Act
        var result = _sut.SetStartup(enable: true);

        // Assert
        Assert.True(result);
        Assert.True(_sut.IsStartupEnabled);
        Assert.True(_sut.LaunchMinimized);
        Assert.True(_provider.IsRegistered(appName));

        var registeredCommand = _provider.GetRegisteredCommand(appName);
        Assert.NotNull(registeredCommand);
        Assert.Equal($"\"{exePath}\" --minimized", registeredCommand);
    }

    [Fact]
    public void SetStartup_EnableWithExplicitMinimizedTrue_RegistersCommandWithMinimizedFlag()
    {
        // Arrange
        var appName = _sut.AppName;
        var exePath = _sut.GetCurrentExecutablePath();

        // Act
        var result = _sut.SetStartup(enable: true, launchMinimized: true);

        // Assert
        Assert.True(result);
        Assert.True(_sut.IsStartupEnabled);
        Assert.True(_sut.LaunchMinimized);

        var registeredCommand = _provider.GetRegisteredCommand(appName);
        Assert.Equal($"\"{exePath}\" --minimized", registeredCommand);
    }

    #endregion

    #region Scenario 2: Enable startup without minimized flag

    [Fact]
    public void SetStartup_EnableWithoutMinimized_RegistersCommandWithoutMinimizedFlagAndSetsProperties()
    {
        // Arrange
        var appName = _sut.AppName;
        var exePath = _sut.GetCurrentExecutablePath();

        // Act
        var result = _sut.SetStartup(enable: true, launchMinimized: false);

        // Assert
        Assert.True(result);
        Assert.True(_sut.IsStartupEnabled);
        Assert.False(_sut.LaunchMinimized);
        Assert.True(_provider.IsRegistered(appName));

        var registeredCommand = _provider.GetRegisteredCommand(appName);
        Assert.NotNull(registeredCommand);
        Assert.Equal($"\"{exePath}\"", registeredCommand);
    }

    #endregion

    #region Scenario 3: Disable startup

    [Fact]
    public void SetStartup_DisableWhenRegistered_RemovesRegistrationAndSetsProperties()
    {
        // Arrange - register first
        _sut.SetStartup(enable: true, launchMinimized: true);
        Assert.True(_provider.IsRegistered(_sut.AppName));

        // Act
        var result = _sut.SetStartup(enable: false);

        // Assert
        Assert.True(result);
        Assert.False(_sut.IsStartupEnabled);
        Assert.False(_provider.IsRegistered(_sut.AppName));
        Assert.Null(_provider.GetRegisteredCommand(_sut.AppName));
    }

    #endregion

    #region Scenario 4: CLI argument detection (StartupArguments.ShouldStartMinimized)

    [Theory]
    [InlineData("--minimized")]
    [InlineData("-minimized")]
    [InlineData("--startup")]
    [InlineData("-startup")]
    [InlineData("--MINIMIZED")]
    [InlineData("-MINIMIZED")]
    [InlineData("--STARTUP")]
    [InlineData("-STARTUP")]
    [InlineData("--Minimized")]
    [InlineData("-Startup")]
    public void StartupArguments_ShouldStartMinimized_ReturnsTrueForValidFlags(string flag)
    {
        // Arrange
        var singleArg = new[] { flag };
        var mixedArgs = new[] { "--other-flag", flag, "positionalArg" };

        // Act & Assert
        Assert.True(StartupArguments.ShouldStartMinimized(singleArg));
        Assert.True(StartupArguments.ShouldStartMinimized(mixedArgs));
    }

    [Fact]
    public void StartupArguments_ShouldStartMinimized_ReturnsFalseForNullOrEmpty()
    {
        // Act & Assert
        Assert.False(StartupArguments.ShouldStartMinimized(null));
        Assert.False(StartupArguments.ShouldStartMinimized(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--other")]
    [InlineData("minimized")]
    [InlineData("startup")]
    [InlineData("--min")]
    [InlineData("--startup-delay")]
    [InlineData("-s")]
    [InlineData("-m")]
    public void StartupArguments_ShouldStartMinimized_ReturnsFalseForUnrelatedFlags(string flag)
    {
        // Arrange
        var args = new[] { flag };

        // Act & Assert
        Assert.False(StartupArguments.ShouldStartMinimized(args));
    }

    #endregion

    #region Scenario 5: Defensive error handling for provider exceptions

    [Fact]
    public void SetStartup_WhenProviderThrowsUnauthorizedAccessExceptionOnRegister_ReturnsFalseGracefully()
    {
        // Arrange
        _provider.ThrowOnRegister = true;
        _provider.RegisterException = new UnauthorizedAccessException("Registry key access denied.");

        // Act
        var result = _sut.SetStartup(enable: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetStartup_WhenProviderThrowsSecurityExceptionOnRegister_ReturnsFalseGracefully()
    {
        // Arrange
        _provider.ThrowOnRegister = true;
        _provider.RegisterException = new SecurityException("Permission denied.");

        // Act
        var result = _sut.SetStartup(enable: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetStartup_WhenProviderThrowsInvalidOperationExceptionOnRegister_ReturnsFalseGracefully()
    {
        // Arrange
        _provider.ThrowOnRegister = true;
        _provider.RegisterException = new InvalidOperationException("Registry hive invalid.");

        // Act
        var result = _sut.SetStartup(enable: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetStartup_WhenProviderThrowsUnauthorizedAccessExceptionOnUnregister_ReturnsFalseGracefully()
    {
        // Arrange
        _sut.SetStartup(enable: true);
        _provider.ThrowOnUnregister = true;
        _provider.UnregisterException = new UnauthorizedAccessException("Cannot delete registry value.");

        // Act
        var result = _sut.SetStartup(enable: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetStartup_WhenProviderThrowsSecurityExceptionOnUnregister_ReturnsFalseGracefully()
    {
        // Arrange
        _sut.SetStartup(enable: true);
        _provider.ThrowOnUnregister = true;
        _provider.UnregisterException = new SecurityException("Cannot delete registry value due to security policy.");

        // Act
        var result = _sut.SetStartup(enable: false);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Security & Reliability Invariant: Path Quoting

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetStartup_AlwaysEncapsulatesExecutablePathInQuotes(bool launchMinimized)
    {
        // Act
        _sut.SetStartup(enable: true, launchMinimized: launchMinimized);

        // Assert
        var command = _provider.GetRegisteredCommand(_sut.AppName);
        Assert.NotNull(command);
        Assert.StartsWith("\"", command);

        var exePath = _sut.GetCurrentExecutablePath();
        if (launchMinimized)
        {
            Assert.Equal($"\"{exePath}\" --minimized", command);
        }
        else
        {
            Assert.Equal($"\"{exePath}\"", command);
        }
    }

    #endregion

    #region Invariants: Idempotency & Safe De-registration

    [Fact]
    public void SetStartup_CalledRepeatedlyWithDifferentMinimizedFlags_UpdatesRegistrationCleanly()
    {
        // Arrange & Act 1: Enable minimized
        var result1 = _sut.SetStartup(enable: true, launchMinimized: true);
        Assert.True(result1);
        Assert.True(_sut.LaunchMinimized);
        Assert.Equal($"\"{_sut.GetCurrentExecutablePath()}\" --minimized", _provider.GetRegisteredCommand(_sut.AppName));

        // Act 2: Enable without minimized (updates existing entry)
        var result2 = _sut.SetStartup(enable: true, launchMinimized: false);
        Assert.True(result2);
        Assert.False(_sut.LaunchMinimized);
        Assert.Equal($"\"{_sut.GetCurrentExecutablePath()}\"", _provider.GetRegisteredCommand(_sut.AppName));

        // Act 3: Enable with minimized again
        var result3 = _sut.SetStartup(enable: true, launchMinimized: true);
        Assert.True(result3);
        Assert.True(_sut.LaunchMinimized);
        Assert.Equal($"\"{_sut.GetCurrentExecutablePath()}\" --minimized", _provider.GetRegisteredCommand(_sut.AppName));
    }

    [Fact]
    public void SetStartup_DisableWhenNotRegistered_ReturnsTrueAndDoesNotThrow()
    {
        // Arrange
        Assert.False(_provider.IsRegistered(_sut.AppName));

        // Act
        var result = _sut.SetStartup(enable: false);

        // Assert
        Assert.True(result);
        Assert.False(_sut.IsStartupEnabled);
        Assert.False(_provider.IsRegistered(_sut.AppName));
    }

    #endregion

    #region State Synchronization: RefreshStatus

    [Fact]
    public void RefreshStatus_WhenRegisteredWithMinimizedFlag_UpdatesStateToEnabledAndMinimizedTrue()
    {
        // Arrange
        _provider.SeedRegistration(_sut.AppName, "\"C:\\Programs\\SmartCalendar\\app.exe\" --minimized");

        // Act
        _sut.RefreshStatus();

        // Assert
        Assert.True(_sut.IsStartupEnabled);
        Assert.True(_sut.LaunchMinimized);
    }

    [Fact]
    public void RefreshStatus_WhenRegisteredWithCaseInsensitiveMinimizedFlag_UpdatesStateCorrectly()
    {
        // Arrange
        _provider.SeedRegistration(_sut.AppName, "\"C:\\Programs\\SmartCalendar\\app.exe\" --MINIMIZED");

        // Act
        _sut.RefreshStatus();

        // Assert
        Assert.True(_sut.IsStartupEnabled);
        Assert.True(_sut.LaunchMinimized);
    }

    [Fact]
    public void RefreshStatus_WhenRegisteredWithoutMinimizedFlag_UpdatesStateToEnabledAndMinimizedFalse()
    {
        // Arrange
        _provider.SeedRegistration(_sut.AppName, "\"C:\\Programs\\SmartCalendar\\app.exe\"");

        // Act
        _sut.RefreshStatus();

        // Assert
        Assert.True(_sut.IsStartupEnabled);
        Assert.False(_sut.LaunchMinimized);
    }

    [Fact]
    public void RefreshStatus_WhenNotRegistered_UpdatesStateToDisabled()
    {
        // Arrange - initially set enabled, then clear provider externally
        _sut.SetStartup(enable: true);
        Assert.True(_sut.IsStartupEnabled);

        _provider.Clear();

        // Act
        _sut.RefreshStatus();

        // Assert
        Assert.False(_sut.IsStartupEnabled);
    }

    #endregion

    #region Default Values & Contract Guarantees

    [Fact]
    public void AppName_DefaultsToSmartCalendarManager()
    {
        // Assert
        Assert.Equal("Smart Calendar Manager", _sut.AppName);
    }

    [Fact]
    public void GetCurrentExecutablePath_ReturnsNonEmptyPath()
    {
        // Act
        var path = _sut.GetCurrentExecutablePath();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(path));
    }

    #endregion
}
