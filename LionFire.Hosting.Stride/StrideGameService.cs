using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stride.Engine;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LionFire.Hosting
{
    public class StrideGameService : IHostedService
    {
        Task runTask;

        public Game Game => game;
        private Game game;


        #region DataContext

        public object DataContext
        {
            get => dataContext;
            set
            {
                if (dataContext == value) return;
                if (dataContext != null && value != null) throw new AlreadySetException("Must first set back to null");
                dataContext = value;
            }
        }
        private object dataContext;

        #endregion

        public IHostApplicationLifetime HostApplicationLifetime { get; }
        public ILogger<StrideGameService> Logger { get; }

        public StrideGameService(IHostApplicationLifetime hostApplicationLifetime, ILogger<StrideGameService> logger)
        {
            HostApplicationLifetime = hostApplicationLifetime;
            Logger = logger;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            runTask = Task.Run(() =>
            {
                using (game = new Game())
                {
                    Task.Run(async () =>
                    {
                        while (game?.Window == null)
                        {
                            Logger.LogDebug("Waiting for Game.Window to appear...");
                            await Task.Delay(200);
                        }
                        Logger.LogDebug("Waiting for Game window to appear...done.");
                        game.Window.Closing += Window_Closing;
                    });
                    game.Run();
                }
            });
            return Task.CompletedTask;
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            Logger.LogInformation($"Received Window Closing event from Stride.  Stopping Application.");
            HostApplicationLifetime.StopApplication();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            game.Exit();
            while (game.IsRunning)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}
