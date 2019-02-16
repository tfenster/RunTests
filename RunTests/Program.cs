using CommandLine;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RunTests
{
    class Program
    {
        static UserContext context = null;


        //static string GetClientServiceEndpoint(string azuretenantId)
        //{
        //    var webClient = new System.Net.WebClient();
        //    var s = webClient.DownloadString($"https://businesscentral.dynamics.com/{azuretenantId}/deployment/url");
        //    var deploymentStatus = JsonConvert.DeserializeObject<DeploymentStatus>(s);
        //    var parts = deploymentStatus.data.Split('?');
        //    return parts[0] + (parts[0].EndsWith("/") ? "" : "/") + "cs?" + parts[1];
        //}

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<WindowsAuthTestSettings, NavUserPasswordAuthTestSettings, AadAuthTestSettings, TestSettingsConnectionFile>(args)
                .MapResult(
                (WindowsAuthTestSettings settings) => RunTestsWindowsAuth(settings),
                (NavUserPasswordAuthTestSettings settings) => RunTestsNavUserPasswordAuth(settings),
                (AadAuthTestSettings settings) => RunTestsAadAuth(settings),
                (TestSettingsConnectionFile settings) => RunTestsConnectionFile(settings),
                errs => 1);
        }

        private static int RunTestsConnectionFile(TestSettingsConnectionFile settings)
        {
            if (!File.Exists(settings.ServiceConnectionFile))
            {
                throw new FileNotFoundException("Cannot locate file with Connection Information", settings.ServiceConnectionFile);
            }
            var authenticationSettings = JsonConvert.DeserializeObject<AuthenticationSetting>(System.IO.File.ReadAllText(settings.ServiceConnectionFile));
            return RunNsysTests(authenticationSettings, settings);
        }

        private static int RunTestsAadAuth(AadAuthTestSettings settings)
        {
            var authenticationSetting = new AuthenticationSetting
            {
                AuthenticationScheme = AuthenticationScheme.AzureActiveDirectory,
                ServiceUrl = settings.ServiceUrl,
                Username = settings.Username,
                Password = settings.Password,
                Authority = settings.Authority,
                Resource = settings.Resource,
                ClientId = settings.ClientId,
                ClientSecret = settings.ClientSecret
            };
            return RunNsysTests(authenticationSetting, settings);
        }

        private static int RunTestsNavUserPasswordAuth(NavUserPasswordAuthTestSettings settings)
        {
            var authenticationSetting = new AuthenticationSetting
            {
                AuthenticationScheme = AuthenticationScheme.UserNamePassword,
                ServiceUrl = settings.ServiceUrl,
                Username = settings.Username,
                Password = settings.Password
            };
            return RunNsysTests(authenticationSetting, settings);
        }

        private static int RunTestsWindowsAuth(WindowsAuthTestSettings settings)
        {
            var authenticationSetting = new AuthenticationSetting
            {
                AuthenticationScheme = AuthenticationScheme.Windows,
                ServiceUrl = settings.ServiceUrl
            };
            return RunNsysTests(authenticationSetting, settings);
        }

        static int RunNsysTests(AuthenticationSetting authenticationSetting, TestSettingsBase settings)
        { 
            context = new UserContext(authenticationSetting);
            var sessionParameters = new ClientSessionParameters
            {
                CultureId = "en-US",
                UICultureId = "en-US"
            };
            Console.WriteLine("Open session");
            context.OpenSession(sessionParameters);

            Console.WriteLine("Warmup");
            CallMethodWithStopwatch(OpenRoleCenterAndCustomerList, "open role center and customer list", context);

            return 0;
        }

        private static void CallMethodWithStopwatch(Action<UserContext> action, string message, UserContext userContext) {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            action(context);
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine("Runtime for " + message + ": " + elapsedTime);
        }

        private static void OpenRoleCenterAndCustomerList(UserContext userContext)
        {
            var rc = context.OpenForm("5162300");
            context.EnsurePage(5162300, rc);

            var custList = context.OpenForm("22");
            context.EnsurePage(22, custList);

            if (custList.State == ClientLogicalFormState.Open)
            {
                context.InvokeInteraction(new CloseFormInteraction(custList));
            }
        }



        static int RunTests(AuthenticationSetting authenticationSetting, TestSettingsBase settings)
        { 
            context = new UserContext(authenticationSetting);
            var sessionParameters = new ClientSessionParameters
            {
                CultureId = "en-US",
                UICultureId = "en-US"
            };
            context.OpenSession(sessionParameters);

            var form = context.OpenForm(settings.TestPage);
            context.EnsurePage(int.Parse(settings.TestPage), form);

            var suiteControl = form.Control("Suite Name");
            suiteControl.SaveValue(settings.TestSuite);

            Console.WriteLine("Running tests");

            var repeater = form.Repeater();
            int index = 0;
            
            while (true)
            {
                if (index >= (repeater.Offset + repeater.DefaultViewport.Count))
                    context.InvokeInteraction(new ScrollRepeaterInteraction(repeater, 1));

                var rowIndex = (int)(index - repeater.Offset);
                if (rowIndex >= repeater.DefaultViewport.Count)
                    break;

                var row = repeater.DefaultViewport[rowIndex];
                var lineType = row.ContainedControls.First(c => c is ClientSelectionControl);

                if (lineType.StringValue == "Codeunit")
                {
                    lineType.Activate();
                    Console.Write(string.Format("  {0} {1} {2} ", lineType.StringValue, row.Control("Codeunit ID").StringValue, row.Control("Name").StringValue));
                    var runAction = form.Action("Run Selected");
                    runAction.Invoke();
                    context.ValidateForm(form);
                    row = repeater.DefaultViewport[rowIndex];

                    var result = row.Control("Result").StringValue;
                    if (result == "Success")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.WriteLine(result);
                }
                else if (lineType.StringValue == "Function")
                {
                    var writeit = settings.Verbose;
                    var result = row.Control("Result").StringValue;
                    if (result == "Success")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        writeit = true;
                    }
                    if (writeit)
                    {
                        Console.WriteLine(string.Format("    {0} {1} {2}", lineType.StringValue, row.Control("Name").StringValue, result));
                    }
                } else
                {
                    Console.WriteLine(string.Format("{0} {1}", lineType.StringValue, row.Control("Name").StringValue));
                }
                Console.ResetColor();

                index++;
            }

            context.CloseAllForms();
            context.CloseSession();

            Console.WriteLine("Done");
            return 0;
        }
    }
}
