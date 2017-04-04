﻿using log4net;
using System;
using System.Reflection;
using Topshelf.HostConfigurators;
using Topshelf.Squirrel.Updater.Builders;
using Topshelf.Squirrel.Updater.Interfaces;

/// <summary>
/// 
/// </summary>
namespace Topshelf.Squirrel.Updater
{
	public class SquirreledHost
	{

        #region Logger

        /// <summary>
        /// Logger Log4Net
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(SquirreledHost));

        #endregion

        /// <summary>
        /// The service name
        /// </summary>
        private readonly string serviceName;

        /// <summary>
        /// The service display name
        /// </summary>
        private readonly string serviceDisplayName;

        /// <summary>
        /// Service RunAs 'Login'
        /// </summary>
        private string serviceRunAsLogin;

        /// <summary>
        /// Service RunAs 'Password'
        /// </summary>
        private string serviceRunAsPassword;

        /// <summary>
        /// The with overlapping
        /// </summary>
        private readonly bool withOverlapping;

        /// <summary>
        /// Run Service AS
        /// </summary>
        private readonly RunAS TypeRunAs;

        /// <summary>
        /// The prompt for credentials while installing
        /// </summary>
        private readonly bool promptForCredentialsWhileInstalling;

        /// <summary>
        /// The self updatable service
        /// </summary>
        private readonly ISelfUpdatableService selfUpdatableService;

        /// <summary>
        /// The updater
        /// </summary>
        private readonly IUpdater updater;

        /// <summary>
        /// Initializes a new instance of the <see cref="SquirreledHost"/> class.
        /// </summary>
        public SquirreledHost(
			ISelfUpdatableService selfUpdatableService, 
			string serviceName = null,
			string serviceDisplayName = null, IUpdater updater = null, bool withOverlapping = false, RunAS pTypeRunAs = RunAS.LocalSystem)
		{
			var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            this.serviceName = serviceName ?? assemblyName;
			this.serviceDisplayName = serviceDisplayName ?? assemblyName;
			this.selfUpdatableService = selfUpdatableService;
			this.withOverlapping = withOverlapping;
            TypeRunAs = pTypeRunAs;
            promptForCredentialsWhileInstalling = false;
            serviceRunAsLogin = "";
            serviceRunAsPassword = "";
            if (pTypeRunAs == RunAS.PromptForCredentials)
            {
                promptForCredentialsWhileInstalling = true;
            }
			this.updater = updater;
		}

        /// <summary>
        /// Set credential of Service User
        /// </summary>
        /// <param name="pLogin"></param>
        /// <param name="pPassword"></param>
        public void SetCredentials(string pLogin, string pPassword)
        {
            serviceRunAsLogin = pLogin;
            serviceRunAsPassword = pPassword;
        }

        /// <summary>
        /// Configures the and run.
        /// </summary>
        /// <param name="configureExt">The configure ext.</param>
        public void ConfigureAndRun(ConfigureExt configureExt = null)
		{
			HostFactory.Run(configurator => { Configure(configurator); configureExt?.Invoke(configurator); });
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config">The configuration.</param>
        public delegate void ConfigureExt(HostConfigurator config);

        /// <summary>
        /// Configures the specified configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        private void Configure(HostConfigurator config)
		{
			config.Service<ISelfUpdatableService>(service =>
			{
				service.ConstructUsing(settings => selfUpdatableService);
				service.WhenStarted((s, hostControl) =>
				{
					s.Start();
					return true;
				});
				service.AfterStartingService(() => { updater?.Start(); });
				service.WhenStopped(s => { s.Stop(); });
			});

			config.SetServiceName(serviceName);
			config.SetDisplayName(serviceDisplayName);
			config.StartAutomatically();
			config.EnableShutdown();

			if (promptForCredentialsWhileInstalling)
			{
				config.RunAsFirstPrompt();
			}
			else
			{
                if (TypeRunAs == RunAS.LocalSystem)
                {
                    config.RunAsLocalSystem();
                }
                else if (TypeRunAs == RunAS.LocalService)
                {
                    config.RunAsLocalService();
                }
                else if (TypeRunAs == RunAS.NetworkService)
                {
                    config.RunAsNetworkService();
                }
                else if (TypeRunAs == RunAS.SpecificUser)
                {
                    config.RunAs(serviceRunAsLogin, serviceRunAsPassword);
                }
            }

			config.AddCommandLineSwitch("squirrel", _ => { });
			config.AddCommandLineDefinition("firstrun", _ => Environment.Exit(0));
			config.AddCommandLineDefinition("obsolete", _ => Environment.Exit(0));
			config.AddCommandLineDefinition("updated", version => { config.UseHostBuilder((env, settings) => new UpdateHostBuilder(env, settings, version, withOverlapping)); });
			config.AddCommandLineDefinition("install", version => { config.UseHostBuilder((env, settings) => new InstallAndStartHostBuilder(env, settings, version)); });
			config.AddCommandLineDefinition("uninstall", _ => { config.UseHostBuilder((env, settings) => new StopAndUninstallHostBuilder(env, settings)); });
		}
	}
}