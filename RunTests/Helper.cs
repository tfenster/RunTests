using System;
using Microsoft.Dynamics.Framework.UI.Client;

namespace RunTests
{

    public class Helper
    {
        public static void WriteForm(ClientLogicalForm form)
        {
            Console.WriteLine(form.Name + " " + form.Caption);
            foreach (var control in form.Children)
            {
                WriteControl(control, 1);
            }
        }

        internal static void WriteControl(ClientLogicalControl control, int indent)
        {
            Console.Write(GetIndentString(indent) + control.Name + " (vis: " + control.Visible + ") ");

            if (control is ClientGroupControl)
            {
                Console.WriteLine(control.MappingHint + " " +control.Caption);
                foreach (var childControl in control.Children)
                {
                    WriteControl(childControl, indent + 1);
                }
            }
            else if (control is ClientStaticStringControl || control is ClientInt32Control || control is ClientStringControl)
            {
                Console.WriteLine(control.StringValue);
            }
            else if (control is ClientActionControl)
            {
                Console.WriteLine(control.Caption);
            }
            else
            {
                Console.WriteLine(control.GetType());
            }
        }

        internal static string GetIndentString(int indentLevel) {
            return new string(' ', indentLevel);
        }
    }
}