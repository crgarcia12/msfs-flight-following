using FSUIPCWinformsAutoCS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MSFSFlightFollowing.Agents;
using MSFSFlightFollowing.AgentsCore;
using MSFSFlightFollowing.Models;
using MSFSFlightFollowing.Runtime;
using MSFSFlightFollowing.SimConnect;
using MSFSFlightFollowing.SimConnect.Detectors;

namespace MSFSFlightFollowing
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        private IWebHostEnvironment Env { get; }

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Env = env;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
            services.Configure<FeatureOptions>(Configuration.GetSection(FeatureOptions.SectionName));

            var mvc = services.AddControllersWithViews();
#if DEBUG
            mvc.AddRazorRuntimeCompilation();
#endif
            services.AddSignalR();

            // ----- Agent bus + capabilities -----
            services.AddSingleton<IAgentBus, AgentBus>();
            services.AddSingleton<SimBridgeClient>();
            services.AddSingleton<ISimBridgeMcdu>(sp => sp.GetRequiredService<SimBridgeClient>());

            // ----- SimConnect -----
            // Register the concrete type so HomeController's health endpoint
            // (and SimDataDispatcher) can still ask for SimConnector directly.
            services.AddSingleton<SimConnector>();
            services.AddSingleton<ISimCommands>(sp => sp.GetRequiredService<SimConnector>());

            // ----- Detectors -----
            services.AddSingleton<FlightPhaseDetector>();
            services.AddSingleton<AltitudeCalloutEmitter>();
            services.AddSingleton<TakeoffDetector>();

            // ----- Runtime glue -----
            services.AddSingleton<AgentContext>(sp => new AgentContext(
                sp.GetRequiredService<IAgentBus>(),
                sp.GetRequiredService<ISimCommands>(),
                sp.GetRequiredService<ISimBridgeMcdu>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MSFSFlightFollowing.Models.FeatureOptions>>().Value.Agents.Enabled));

            services.AddSingleton<AgentBase, Comms>();
            services.AddSingleton<AgentBase, Operations>();
            services.AddSingleton<AgentBase, Navigator>();
            services.AddSingleton<AgentBase, Copilot>();
            services.AddSingleton<AgentBase, Pilot>();
            // AltitudeGuard needs to be resolvable by concrete type so the recover-alt
            // controller endpoint can call its RecoverAltitudeNow() method.
            services.AddSingleton<AltitudeGuard>();
            services.AddSingleton<AgentBase>(sp => sp.GetRequiredService<AltitudeGuard>());

            // DescentGuard — auto-fires the descent when FBW VNAV raises T/D REACHED.
            // Resolvable as concrete type so the /api/sim/begin-descent endpoint can
            // call its public BeginDescentNow().
            services.AddSingleton<DescentGuard>();
            services.AddSingleton<AgentBase>(sp => sp.GetRequiredService<DescentGuard>());

            services.AddSingleton<SignalRAgentBridge>();
            services.AddSingleton<EventHub>();

            // ----- VATSIM -----
            services.AddSingleton<MSFSFlightFollowing.Vatsim.VatsimDataClient>();
            services.AddSingleton<VatsimSignalRBridge>();

            // SimDataDispatcher is a BackgroundService.
            services.AddHostedService<SimDataDispatcher>();
            // SimRuntimeHostedService kicks off the sim once everyone is subscribed.
            services.AddHostedService<SimRuntimeHostedService>();
            // VatsimWatcher polls data.vatsim.net (no-op when Features.Vatsim.Enabled = false).
            services.AddHostedService<MSFSFlightFollowing.Vatsim.VatsimWatcher>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapHub<WebSocketConnector>("/ws");
            });
        }
    }
}
