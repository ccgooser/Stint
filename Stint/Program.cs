using Stint.Data;

namespace Stint
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Database.Initialize();

            var instance = new SingleInstance();

            if (!instance.IsFirstInstance)
            {
                // Forward URL to running instance and exit
                if (args.Length > 0)
                    SingleInstance.SendUrl(args[0]);
                instance.Dispose();
                return;
            }

            // First instance — register protocol and start listening
            ProtocolHandler.Register();

            var mainForm = new MainForm();
            var _ = mainForm.Handle;

            var (_, wasOpen) = TimerPopupForm.LoadRegistryState();
            if (wasOpen) mainForm.OpenPopup();

            // Wire up URL dispatcher
            var dispatcher = new UrlDispatcher(new Stint.Data.EventRepository(), mainForm);
            instance.StartListening();
            instance.UrlReceived += url => dispatcher.Dispatch(url);

            Application.Run(mainForm);
            instance.Dispose();
        }
    }
}
