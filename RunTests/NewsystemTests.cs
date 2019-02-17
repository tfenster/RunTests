using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;

namespace RunTests {
    class NewsystemTests {
        static UserContext context = null;
        const int RoleCenterId = 5162300;
        const int CustomerListPageId = 22;
        const int BerichtsdefinitionenPageId = 5010603;

        public static int RunTests(AuthenticationSetting authenticationSetting, TestSettingsBase settings)
        { 
            context = new UserContext(authenticationSetting);
            var sessionParameters = new ClientSessionParameters
            {
                CultureId = "de-DE",
                UICultureId = "de-DE"
            };
            Console.WriteLine("Open session");
            context.OpenSession(sessionParameters);

            Console.WriteLine("Warmup");
            CallMethodWithStopwatch(OpenRoleCenterAndCustomerList, "open role center and customer list");
            
            Console.WriteLine("Run");
            CallMethodWithStopwatch(OpenRoleCenterAndCustomerList, "open role center and customer list");
            CallMethodWithStopwatch(CalcHHPlan, "calc HHPlan");

            context.CloseSession();
            return 0;
        }

        private static void CallMethodWithStopwatch(Action<UserContext> action, string message) {
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
            var rc = context.OpenForm("" + RoleCenterId);
            context.EnsurePage(RoleCenterId, rc);

            var custList = context.OpenForm("" + CustomerListPageId);
            context.EnsurePage(CustomerListPageId, custList);

            ClosePage(custList);
        }

        private static void CalcHHPlan(UserContext userContext)
        {
            var berichtsdefinitionenForm = context.OpenForm("" + BerichtsdefinitionenPageId);
            context.EnsurePage(BerichtsdefinitionenPageId, berichtsdefinitionenForm);
            ExecuteQuickFilter(berichtsdefinitionenForm, "Code", "HHKURZ2011"); // HHPLAN2015
            var berichtsdefinitionForm = berichtsdefinitionenForm.Action("Ansicht").InvokeCatchForm();
            ClientLogicalForm confirmDialog = userContext.CatchDialog(berichtsdefinitionForm.Action("Daten berechnen").Invoke);
            //var confirmDialog = berichtsdefinitionForm.Action("Daten berechnen").InvokeCatchDialog();
            
            Task.Run(() =>
            {
                while (confirmDialog.Session.IsReadyOrBusy())
                {
                    Console.WriteLine("send keepalive");
                    confirmDialog.Session.KeepAliveAsync();
                    Thread.Sleep(60 * 1000);
                }
            });
            //var progressDialog = confirmDialog.Action("Ja").InvokeCatchDialog();
            
            confirmDialog.Session.AwaitReady(() => { }, session => session.State == ClientSessionState.Ready, false, -1);
            ClosePage(berichtsdefinitionForm);
            ClosePage(berichtsdefinitionenForm);
        }

        internal static void ExecuteQuickFilter(ClientLogicalForm form, string columnName, string value)
        {
            var filter = form.FindLogicalFormControl<ClientFilterLogicalControl>();
            form.Session.InvokeInteraction(new FilterInteraction(filter) 
            {
              FilterColumnId = filter.FilterColumns.First(columnDef => columnDef.Caption.Replace("&", "").Equals(columnName)).Id,
              FilterValue = value
            });

            if (filter.ValidationResults.Count > 0)
            {
              throw new ArgumentException("Could not execute filter.");
            }
        }

        internal static void ClosePage(ClientLogicalForm form) {
            if (form.State == ClientLogicalFormState.Open)
            {
                context.InvokeInteraction(new CloseFormInteraction(form));
            }
        }
    }
}