#region

using System;
using System.Windows.Forms;
using log4net;
using log4net.Config;
using MsCommon.ClickOnce;

#endregion

namespace AvroViewerGui
{
    static class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arguments)
        {
            Action<string[]> method = args =>
            {
                XmlConfigurator.Configure();
                Logger.Info("Starting...");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var arg = AppDomain.CurrentDomain.SetupInformation?.ActivationArguments?.ActivationData?[0];

                var form = new MainForm(arg);
                Application.Run(form);
            };

            AppProgram.Start(
                applicationName: "AvroViewer",
                authorName: "Martijn Stolk",
                reportBugEndpoint: "http://martijn.tikkie.net/reportbug.php",
                feedbackEndpoint: "http://martijn.tikkie.net/feedback.php",
                args: arguments,
                mainMethod: method);
        }
    }
}