﻿using System;
using System.Linq;
using System.Web;
using Kamsar.WebConsole;
using Sitecore.Pipelines;
using Unicorn.Configuration;
using Unicorn.ControlPanel.Headings;
using Unicorn.Logging;
using Unicorn.Pipelines.UnicornSyncEnd;
using Unicorn.Predicates;

namespace Unicorn.ControlPanel
{
	/// <summary>
	/// Runs a Unicorn sync in a WebConsole of a configuration or configurations
	/// </summary>
	public class SyncConsole : ControlPanelConsole
	{
		private readonly IConfiguration[] _configurations;

		public SyncConsole(bool isAutomatedTool, IConfiguration[] configurations)
			: base(isAutomatedTool, new HeadingService())
		{
			_configurations = configurations;
		}

		protected override string Title
		{
			get { return "Sync Unicorn"; }
		}

		protected override void Process(IProgressStatus progress)
		{
			var configurations = ResolveConfigurations();
			int taskNumber = 1;

			foreach (var configuration in configurations)
			{
				var logger = configuration.Resolve<ILogger>();
				var helper = configuration.Resolve<SerializationHelper>();

				using (new LoggingContext(new WebConsoleLogger(progress), configuration))
				{
					try
					{
						logger.Info(string.Empty);
						logger.Info(configuration.Name + " is being synced.");

						using (new TransparentSyncDisabler())
						{
							var pathResolver = configuration.Resolve<PredicateRootPathResolver>();

							var roots = pathResolver.GetRootSerializedItems();

							var index = 0;

							helper.SyncTree(configuration, item =>
							{
								SetTaskProgress(progress, taskNumber, configurations.Length, (int)((index / (double)roots.Length) * 100));
								index++;
							}, roots);
						}
					}
					catch (Exception ex)
					{
						logger.Error(ex);
						break;
					}
				}

				taskNumber++;
			}

			CorePipeline.Run("unicornSyncEnd", new UnicornSyncEndPipelineArgs(configurations));
		}

		protected virtual IConfiguration[] ResolveConfigurations()
		{
			var config = HttpContext.Current.Request.QueryString["configuration"];
			var targetConfigurations = ControlPanelUtility.ResolveConfigurationsFromQueryParameter(config);

			if (targetConfigurations.Length == 0) throw new ArgumentException("Configuration(s) requested were not defined.");

			return targetConfigurations;
		}
	}
}
