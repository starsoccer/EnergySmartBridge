using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Mono.Unix;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace EnergySmartBridge
{
    class Program
    {
        static CoreServer server;

        static void Main(string[] args)
        {
            bool interactive = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "/?":
                    case "-h":
                    case "-help":
                        ShowHelp();
                        return;
                    case "-c":
                        Global.config_file = args[++i];
                        break;
                    case "-e":
                        Settings.UseEnvironment = true;
                        break;
                    case "-d":
                        Settings.ShowDebug = true;
                        break;
                    case "-i":
                        interactive = true;
                        break;
                }
            }

            if (string.IsNullOrEmpty(Global.config_file))
                Global.config_file = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) +
                    Path.DirectorySeparatorChar + "EnergySmartBridge.ini";

            XmlConfigurator.Configure();
            if (Environment.UserInteractive || interactive)
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            if (Settings.ShowDebug)
            {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.Level = Level.Debug;
                hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
            }

            try
            {
                Settings.LoadSettings(Global.config_file);
            }
            catch
            {
                // Errors are logged in LoadSettings();
                Environment.Exit(1);
            }

            if (Environment.UserInteractive || interactive)
            {
                if (IsRunningOnMono())
                {
                    UnixSignal[] signals = new UnixSignal[]{
                        new UnixSignal(Mono.Unix.Native.Signum.SIGTERM),
                        new UnixSignal(Mono.Unix.Native.Signum.SIGINT),
                        new UnixSignal(Mono.Unix.Native.Signum.SIGUSR1)
                    };

                    Task.Factory.StartNew(() =>
                    {
                        // Blocking call to wait for any kill signal
                        int index = UnixSignal.WaitAny(signals, -1);

                        server.Shutdown();
                    });
                }

                Console.TreatControlCAsInput = false;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);

                server = new CoreServer();
            }
            else
            {
                ServiceBase[] ServicesToRun;

                // More than one user Service may run within the same process. To add
                // another service to this process, change the following line to
                // create a second service object. For example,
                //
                //   ServicesToRun = new ServiceBase[] {new Service1(), new MySecondUserService()};
                //
                ServicesToRun = new ServiceBase[] { new Service() };

                ServiceBase.Run(ServicesToRun);
            }
        }

        protected static void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            server.Shutdown();
            args.Cancel = true;
        }

        static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        static void ShowHelp()
        {
            Console.WriteLine(
                AppDomain.CurrentDomain.FriendlyName + " [-c config_file] [-e] [-d] [-s subscriptions_file] [-i]\n" +
                "\t-c Specifies the configuration file. Default is EnergySmartBridge.ini\n" +
                "\t-e Check environment variables for configuration settings\n" +
                "\t-d Show debug output for configuration loading\n" +
                "\t-i Run in interactive mode");
        }
    }
}
