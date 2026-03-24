using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Ct3xxProgramParser.Discovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Ct3xxSimulator.WinAppDriverTests.Infrastructure;

public abstract class WinAppDriverTestBase
{
    private const string DefaultWinAppDriverUrl = "http://127.0.0.1:4723";
    private WindowsDriver? _session;
    private string? _mainWindowHandle;
    private string? _applicationPath;
    private string? _programPath;

    public TestContext? TestContext { get; set; }
    protected WindowsDriver Session => _session ?? throw new InvalidOperationException("Session not initialized.");
    protected string MainWindowHandle => _mainWindowHandle ?? Session.CurrentWindowHandle;
    protected string ApplicationPath => _applicationPath ?? throw new InvalidOperationException("Application path not resolved.");
    protected string ProgramPath => _programPath ?? throw new InvalidOperationException("Program path not resolved.");

    [TestInitialize]
    public void InitializeTest()
    {
        _applicationPath = ResolveApplicationPath();
        _programPath = ResolveProgramPath(_applicationPath);
        var driverUri = Environment.GetEnvironmentVariable("WINAPPDRIVER_URL") ?? DefaultWinAppDriverUrl;

        var options = new AppiumOptions();
        options.PlatformName = "Windows";
        options.DeviceName = "WindowsPC";
        options.App = _applicationPath;
        options.AddAdditionalAppiumOption("ms:waitForAppLaunch", "15");

        _session = new WindowsDriver(new Uri(driverUri), options, TimeSpan.FromSeconds(60));
        _session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
        _mainWindowHandle = _session.CurrentWindowHandle;
    }

    [TestCleanup]
    public void CleanupTest()
    {
        try
        {
            try
            {
                _session?.CloseApp();
            }
            catch (WebDriverException)
            {
            }

            _session?.Quit();
        }
        finally
        {
            _session = null;
            _mainWindowHandle = null;
        }
    }

    protected AppiumElement WaitForElementByAccessibilityId(string automationId, int timeoutSeconds = 10)
    {
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var element = Session.FindElement(MobileBy.AccessibilityId(automationId));
                if (element != null)
                {
                    return (AppiumElement)element;
                }
            }
            catch (WebDriverException)
            {
            }

            Thread.Sleep(250);
        }

        Assert.Fail($"Element with AutomationId '{automationId}' not found.");
        throw new InvalidOperationException();
    }

    protected IReadOnlyCollection<string> WaitForWindowHandles(int timeoutSeconds = 10)
    {
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < timeout)
        {
            var handles = Session.WindowHandles;
            if (handles.Count > 0)
            {
                return handles;
            }

            Thread.Sleep(250);
        }

        Assert.Fail("No window handles returned. Ensure the CT3xx desktop window stays open.");
        throw new InvalidOperationException();
    }

    protected string WaitForAdditionalWindowHandle(IReadOnlyCollection<string> existingHandles, int timeoutSeconds = 10)
    {
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < timeout)
        {
            var handles = Session.WindowHandles;
            var extraHandle = handles.FirstOrDefault(handle => !existingHandles.Contains(handle));
            if (!string.IsNullOrWhiteSpace(extraHandle))
            {
                return extraHandle;
            }

            Thread.Sleep(250);
        }

        Assert.Fail("Expected additional window handle was not found.");
        throw new InvalidOperationException();
    }

    protected void SwitchToMainWindow()
    {
        Session.SwitchTo().Window(MainWindowHandle);
    }

    protected void WaitUntil(Func<bool> condition, TimeSpan timeout, string failureMessage)
    {
        var limit = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < limit)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(200);
        }

        Assert.Fail(failureMessage);
    }

    protected static string? TryFindTestProgramRoot() => TestProgramDiscovery.FindRoot(AppContext.BaseDirectory);

    protected static string ResolveApplicationPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CT3XX_APP_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var relative = Path.Combine("Ct3xxSimulator.Desktop", "bin", "Debug", "net9.0-windows10.0.19041.0", "Ct3xxSimulator.Desktop.exe");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var candidate = Path.Combine(root, relative);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Unable to locate desktop application. Set CT3XX_APP_PATH or build the desktop project first. Looked for '{candidate}'.");
        }

        return candidate;
    }

    protected static string ResolveProgramPath(string appPath)
    {
        var directory = Path.GetDirectoryName(appPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Unable to resolve application directory.");
        }

        var candidate = Path.Combine(directory, "CT3xx Testadapter - Tutor MDA1 SC3 with Handling.ctxprg");
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Unable to locate CTX program file required for automation. Looked for '{candidate}'.");
        }

        return candidate;
    }
}
