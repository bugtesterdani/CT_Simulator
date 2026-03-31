// Provides Startup Tests for the WinAppDriver test project support code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Ct3xxProgramParser.Discovery;
using Ct3xxSimulator.WinAppDriverTests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Ct3xxSimulator.WinAppDriverTests;

[TestClass]
/// <summary>
/// Represents the startup tests.
/// </summary>
public sealed class StartupTests : WinAppDriverTestBase
{
    [TestMethod]
    [TestCategory("Smoke")]
    /// <summary>
    /// Executes main window title should contain product name.
    /// </summary>
    public void MainWindowTitleShouldContainProductName()
    {
        var title = Session.Title ?? string.Empty;
        StringAssert.Contains(title, "CT3xx Visual Simulator", "Unexpected window title. Make sure the desktop app built successfully.");
    }

    [TestMethod]
    [TestCategory("Smoke")]
    /// <summary>
    /// Executes window handle should exist.
    /// </summary>
    public void WindowHandleShouldExist()
    {
        var handles = WaitForWindowHandles();
        Assert.IsTrue(handles.Count > 0, "No window handles returned. Ensure the CT3xx desktop window stays open.");
    }

    [TestMethod]
    [TestCategory("Simulation")]
    [DynamicData(nameof(GetSamplePrograms))]
    /// <summary>
    /// Executes simulation should complete for sample program.
    /// </summary>
    public void SimulationShouldCompleteForSampleProgram(string programPath, string displayName)
    {
        TestContext?.WriteLine($"Simulation fÃ¼r: {displayName}");
        LoadProgramViaUi(programPath, displayName);
        SeedMeasurementQueue(20);
        StartSimulation();
        WaitForSimulationToFinish();
        AssertLogContains("Simulation abgeschlossen", TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Executes GetSamplePrograms.
    /// </summary>
    private static IEnumerable<object[]> GetSamplePrograms()
    {
        var root = TestProgramDiscovery.FindRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Der Ordner 'testprogramme' wurde nicht gefunden.");
        }

        var programs = TestProgramDiscovery.EnumeratePrograms(root);
        if (programs.Count == 0)
        {
            throw new InvalidOperationException($"Keine CTX-Programme unter '{root}' gefunden.");
        }

        foreach (var program in programs)
        {
            yield return new object[] { program.FilePath, program.DisplayName };
        }
    }

    /// <summary>
    /// Executes LoadProgramViaUi.
    /// </summary>
    private void LoadProgramViaUi(string programPath, string displayName)
    {
        if (TrySelectProgramFromList(displayName))
        {
            WaitUntil(() => WaitForElementByAccessibilityId("LoadSampleProgramButton").Enabled, TimeSpan.FromSeconds(5), "Sample program load button did not enable.");
            WaitForElementByAccessibilityId("LoadSampleProgramButton").Click();
        }
        else
        {
            EnterProgramPath(programPath);
            WaitForElementByAccessibilityId("LoadProgramButton").Click();
        }

        WaitUntil(() => WaitForElementByAccessibilityId("StartSimulationButton").Enabled, TimeSpan.FromSeconds(10), "Start button did not enable after loading program.");
    }

    /// <summary>
    /// Executes TrySelectProgramFromList.
    /// </summary>
    private bool TrySelectProgramFromList(string displayName)
    {
        try
        {
            WaitForElementByAccessibilityId("SampleProgramList");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var item = Session.FindElement(By.Name(displayName));
                    item.Click();
                    return true;
                }
                catch (WebDriverException)
                {
                    Thread.Sleep(200);
                }
            }

            TestContext?.WriteLine($"Eintrag '{displayName}' wurde nicht gefunden.");
            return false;
        }
        catch (WebDriverException ex)
        {
            TestContext?.WriteLine($"SampleProgramList nicht verfÃ¼gbar ({ex.Message}).");
            return false;
        }
    }

    /// <summary>
    /// Executes EnterProgramPath.
    /// </summary>
    private void EnterProgramPath(string programPath)
    {
        var pathBox = WaitForElementByAccessibilityId("ProgramPathBox");
        pathBox.Clear();
        pathBox.SendKeys(programPath);
    }

    /// <summary>
    /// Executes SeedMeasurementQueue.
    /// </summary>
    private void SeedMeasurementQueue(int pairs)
    {
        for (var i = 0; i < pairs; i++)
        {
            WaitForElementByAccessibilityId("AddFailMeasurementButton").Click();
            WaitForElementByAccessibilityId("AddPassMeasurementButton").Click();
        }
    }

    /// <summary>
    /// Executes StartSimulation.
    /// </summary>
    private void StartSimulation()
    {
        WaitForElementByAccessibilityId("StartSimulationButton").Click();
    }

    /// <summary>
    /// Executes WaitForSimulationToFinish.
    /// </summary>
    private void WaitForSimulationToFinish()
    {
        var timeout = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < timeout)
        {
            HandleModalDialogs();
            ClickDisplayConfirmationIfVisible();

            var startButton = WaitForElementByAccessibilityId("StartSimulationButton");
            if (startButton.Enabled)
            {
                return;
            }

            Thread.Sleep(500);
        }

        Assert.Fail("Simulation did not finish before the timeout elapsed.");
    }

    /// <summary>
    /// Executes HandleModalDialogs.
    /// </summary>
    private void HandleModalDialogs()
    {
        var handles = Session.WindowHandles;
        foreach (var handle in handles)
        {
            if (handle == MainWindowHandle)
            {
                continue;
            }

            Session.SwitchTo().Window(handle);
            if (TryHandleInputDialog())
            {
                continue;
            }

            if (TryHandleSelectionDialog())
            {
                continue;
            }

            if (TryHandleGenericOk(out var dialogText))
            {
                if (IsErrorDialogText(dialogText))
                {
                    Assert.Fail($"Simulator meldete einen Fehlerdialog: {dialogText}");
                }

                if (!string.IsNullOrWhiteSpace(dialogText))
                {
                    TestContext?.WriteLine($"Dialog geschlossen: {dialogText}");
                }

                continue;
            }
        }

        SwitchToMainWindow();
    }

    /// <summary>
    /// Executes TryHandleInputDialog.
    /// </summary>
    private bool TryHandleInputDialog()
    {
        try
        {
            var inputBox = Session.FindElement(MobileBy.AccessibilityId("PromptInputBox"));
            var promptLabel = SafeGetText("PromptLabel");
            inputBox.Clear();
            inputBox.SendKeys(ResolveInputValue(promptLabel));
            Session.FindElement(MobileBy.AccessibilityId("PromptOkButton")).Click();
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// Executes TryHandleSelectionDialog.
    /// </summary>
    private bool TryHandleSelectionDialog()
    {
        try
        {
            var list = Session.FindElement(MobileBy.AccessibilityId("SelectionListBox"));
            var items = list.FindElements(By.ClassName("ListBoxItem"));
            if (items.Count > 0)
            {
                items[0].Click();
            }

            Session.FindElement(MobileBy.AccessibilityId("SelectionOkButton")).Click();
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// Executes TryHandleGenericOk.
    /// </summary>
    private bool TryHandleGenericOk(out string? dialogText)
    {
        dialogText = null;
        try
        {
            var okButtons = Session.FindElements(By.Name("OK"));
            if (okButtons.Count == 0)
            {
                return false;
            }

            dialogText = ExtractDialogText();
            okButtons[0].Click();
            return true;
        }
        catch (WebDriverException)
        {
            dialogText = null;
            return false;
        }
    }

    /// <summary>
    /// Executes ExtractDialogText.
    /// </summary>
    private string? ExtractDialogText()
    {
        try
        {
            var source = Session.PageSource;
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            var doc = XDocument.Parse(source);
            var names = doc.Descendants()
                .Select(x => (string?)x.Attribute("Name"))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.Trim())
                .Where(n => n.Length > 0)
                .Take(6)
                .ToList();

            return names.Count == 0 ? null : string.Join(" | ", names);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes IsErrorDialogText.
    /// </summary>
    private static bool IsErrorDialogText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var keywords = new[] { "fehler", "error", "fehlgeschlagen", "ungÃ¼ltig", "nicht mÃ¶glich" };
        var normalized = text.ToLowerInvariant();
        return keywords.Any(normalized.Contains);
    }

    /// <summary>
    /// Executes ClickDisplayConfirmationIfVisible.
    /// </summary>
    private void ClickDisplayConfirmationIfVisible()
    {
        try
        {
            var button = Session.FindElement(MobileBy.AccessibilityId("DisplayConfirmButton"));
            if (button.Displayed && button.Enabled)
            {
                button.Click();
            }
        }
        catch (WebDriverException)
        {
        }
    }

    /// <summary>
    /// Executes AssertLogContains.
    /// </summary>
    private void AssertLogContains(string expectedText, TimeSpan timeout)
    {
        var limit = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < limit)
        {
            try
            {
                var logList = Session.FindElement(MobileBy.AccessibilityId("LogList"));
                var items = logList.FindElements(By.ClassName("ListBoxItem"));
                if (items.Any(item => (item.Text ?? string.Empty).IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return;
                }
            }
            catch (WebDriverException)
            {
            }

            Thread.Sleep(500);
        }

        Assert.Fail($"Log entry '{expectedText}' was not found.");
    }

    /// <summary>
    /// Executes SafeGetText.
    /// </summary>
    private string SafeGetText(string automationId)
    {
        try
        {
            return Session.FindElement(MobileBy.AccessibilityId(automationId)).Text ?? string.Empty;
        }
        catch (WebDriverException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Executes ResolveInputValue.
    /// </summary>
    private static string ResolveInputValue(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "1";
        }

        if (prompt.IndexOf("spann", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "10";
        }

        if (prompt.IndexOf("am2", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "1";
        }

        return "1";
    }
}
