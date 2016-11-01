using System;
using Autofac;
using Ncqrs.Fluent;

namespace Ncqrs.Config.Autofac.Fluent
{
	internal class AutofacConfigurationBuilder : INcqrsConfig
	{
		private readonly IContainer container;
		private INcqrs ncqrs;

		public AutofacConfigurationBuilder(INcqrs ncqrs, IContainer container)
		{
			this.ncqrs = ncqrs;
			this.container = container;
		}

		public void AddRegistration<T>(Registration<T> registration)
		{
			
		}
	}
}