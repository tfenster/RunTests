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
        
        const int ExplDebitorenpostenPageId = 5010440;

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
            CallMethodWithStopwatch(UseExplCustLedgerEntries, "use explorer cust ledger entries");
            
            Console.WriteLine("Run");
            CallMethodWithStopwatch(OpenRoleCenterAndCustomerList, "open role center and customer list");
            CallMethodWithStopwatch(UseExplCustLedgerEntries, "use explorer cust ledger entries");
            CallMethodWithStopwatch(CalcHHPlanKurz, "calc HHPlan kurz");
            CallMethodWithStopwatch(CalcHHPlanLang, "calc HHPlan lang");

            Console.WriteLine("Done");

            context.CloseSession();
            return 0;
        }

        private static void CallMethodWithStopwatch(Action<UserContext> action, string message) {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("Starting " + message + " at " + DateTime.Now.ToShortTimeString());
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

        private static void UseExplCustLedgerEntries(UserContext userContext)
        {
            var explDebPos = context.OpenForm("" + ExplDebitorenpostenPageId);
            context.EnsurePage(ExplDebitorenpostenPageId, explDebPos);

            //explDebPos.Control("Sofortsuche aktiv").SaveValue(true);
            explDebPos.Control("Suchbegriff Debitor").SaveValue("*AN");
            explDebPos.Control("Buchungsdatum").SaveValue("010114..");
            explDebPos.Action("Suchen").Invoke();

            var column = explDebPos.Repeater().Columns.Where(col => col.Caption == "Restbetrag").First();
            column.Action("Absteigend").Invoke();
            column.Action("Aufsteigend").Invoke();

            ClosePage(explDebPos);
        }

        private static void DummySleeper(AuthenticationSetting authenticationSettings) {
            context = new UserContext(authenticationSettings);
            var sessionParameters = new ClientSessionParameters
            {
                CultureId = "en-US",
                UICultureId = "en-US"
            };
            Console.WriteLine("Open session");
            context.OpenSession(sessionParameters);

            var explDebPos = context.OpenForm("50003");
            context.EnsurePage(50003, explDebPos);
            explDebPos.Action("GoAction").Invoke();

            context.CloseSession();
        }

        private static void CalcHHPlanKurz(UserContext userContext)
        {
            CalcHHPlan(userContext, "HHKURZ2011");
        }

        private static void CalcHHPlanLang(UserContext userContext)
        {
            CalcHHPlan(userContext, "HHPLAN2015");
        }

        private static void CalcHHPlan(UserContext userContext, string plan)
        {
            var berichtsdefinitionenForm = context.OpenForm("" + BerichtsdefinitionenPageId);
            context.EnsurePage(BerichtsdefinitionenPageId, berichtsdefinitionenForm);
            ExecuteQuickFilter(berichtsdefinitionenForm, "Code", plan);
            var berichtsdefinitionForm = berichtsdefinitionenForm.Action("Ansicht").InvokeCatchForm();
            var confirmDialog = berichtsdefinitionForm.Action("Daten berechnen").InvokeCatchDialog();
            if (confirmDialog != null)
                confirmDialog.Action("Ja").Invoke();
            context.ClientSession.AwaitReady(() => { }, session => session.State == ClientSessionState.Ready, false, -1);
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