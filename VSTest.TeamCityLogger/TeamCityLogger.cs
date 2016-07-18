using System;
using System.Diagnostics;
using System.IO;
using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace VSTest.TeamCityLogger
{
    [ExtensionUri("logger://TeamCityLogger")]
    [FriendlyName("TeamCity")]
    public class TeamCityLogger : ITestLogger
    {
        private string _currentAssembly;
        private ITeamCityWriter _teamCityWriter;
        private ITeamCityTestsSubWriter _vsTestSuite;
        private ITeamCityTestsSubWriter _currentAssemblySuite;
        private bool _opened;

        public TeamCityLogger()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="testRunDirectory">Test Run Directory</param>
        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            // Register for the events.
            events.TestRunMessage += TestMessageHandler;
            events.TestResult += TestResultHandler;
            events.TestRunComplete += TestRunCompleteHandler;
            _teamCityWriter = new TeamCityServiceMessages().CreateWriter(w => Trace.WriteLine(w));
        }

        /// <summary>
        /// Called when a test message is received.
        /// </summary>
        private void TestMessageHandler(object sender, TestRunMessageEventArgs e)
        {
            EnsureOpened();
            try
            {
                switch (e.Level)
                {
                    case TestMessageLevel.Informational:
                        _teamCityWriter.WriteMessage(e.Message);
                        break;
                    case TestMessageLevel.Warning:
                        _teamCityWriter.WriteWarning(e.Message);
                        break;
                    case TestMessageLevel.Error:
                        _teamCityWriter.WriteError(e.Message);
                        break;
                }
            }
            catch (Exception ex)
            {
                _teamCityWriter.WriteError("TeamCity Logger Error", ex.ToString());
            }
        }

        /// <summary>
        /// Called when a test result is received.
        /// </summary>
        private void TestResultHandler(object sender, TestResultEventArgs e)
        {
            EnsureOpened();
            try
            {
                var currentAssembly = Path.GetFileName(e.Result.TestCase.Source);
                if (_currentAssembly != currentAssembly)
                {
                    if (!string.IsNullOrEmpty(_currentAssembly))
                        _currentAssemblySuite.Dispose();

                    _currentAssembly = currentAssembly;
                    _currentAssemblySuite = _vsTestSuite.OpenTestSuite(_currentAssembly);
                }

                using (var currentTest = _currentAssemblySuite.OpenTest(e.Result.TestCase.FullyQualifiedName))
                {
                    if (e.Result.Outcome == TestOutcome.Skipped)
                    {
                        currentTest.WriteIgnored(e.Result.ErrorMessage);
                    }
                    else if (e.Result.Outcome == TestOutcome.Failed)
                    {
                        currentTest.WriteFailed(e.Result.ErrorMessage, e.Result.ErrorStackTrace);
                    }

                    foreach (var message in e.Result.Messages)
                    {
                        if (!"StdOutMsgs".Equals(message.Category, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        currentTest.WriteStdOutput(message.Text);
                    }
                                        
                    currentTest.WriteDuration(e.Result.Duration);
                }
            }
            catch (Exception ex)
            {
                _teamCityWriter.WriteError("TeamCity Logger Error", ex.ToString());
            }
        }

        /// <summary>
        /// Called when a test run is completed.
        /// </summary>
        private void TestRunCompleteHandler(object sender, TestRunCompleteEventArgs e)
        {
            if (_currentAssemblySuite != null)
            {
                _currentAssemblySuite.Dispose();
            }
            _vsTestSuite.Dispose();
            _teamCityWriter.Dispose();

            Trace.WriteLine(string.Format("Total Executed: {0}", e.TestRunStatistics.ExecutedTests));
            Trace.WriteLine(string.Format("Total Passed: {0}", e.TestRunStatistics[TestOutcome.Passed]));
            Trace.WriteLine(string.Format("Total Failed: {0}", e.TestRunStatistics[TestOutcome.Failed]));
            Trace.WriteLine(string.Format("Total Skipped: {0}", e.TestRunStatistics[TestOutcome.Skipped]));
        }

        /// <summary>
        /// We should not initialize (and open) wrapping test suite until tests have actually started executing.
        /// If there is an error in vstest configuration (for example, runsettings file does not exist),
        /// test logger becomes initialized, but exits without closing root messages flow. This causes build log tree
        /// to break
        /// </summary>
        private void EnsureOpened()
        {
            if (_opened) return;
            _vsTestSuite = _teamCityWriter.OpenTestSuite("VSTest");
            _opened = true;
        }
    }
}
