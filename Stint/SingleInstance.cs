using System.IO.Pipes;

namespace Stint
{
    public class SingleInstance : IDisposable
    {
        private const string MutexName = "Stint_SingleInstance";
        private const string PipeName  = "Stint_IPC";

        private Mutex? _mutex;
        private Thread? _pipeThread;
        private bool _isFirstInstance;
        private CancellationTokenSource _cts = new();

        public bool IsFirstInstance => _isFirstInstance;
        public event Action<string>? UrlReceived;

        public SingleInstance()
        {
            _mutex = new Mutex(true, MutexName, out _isFirstInstance);
        }

        // Called by the first instance to start listening for URLs
        public void StartListening()
        {
            _pipeThread = new Thread(ListenLoop) { IsBackground = true, Name = "IPC Pipe Listener" };
            _pipeThread.Start();
        }

        private void ListenLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In,
                        1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);

                    server.WaitForConnection();

                    using var reader = new StreamReader(server);
                    var url = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(url))
                        UrlReceived?.Invoke(url);
                }
                catch (OperationCanceledException) { break; }
                catch { /* pipe reset, loop and listen again */ }
            }
        }

        // Called by subsequent instances to forward their URL to the running instance
        public static bool SendUrl(string url)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(2000); // 2 second timeout
                using var writer = new StreamWriter(client);
                writer.WriteLine(url);
                writer.Flush();
                return true;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}
