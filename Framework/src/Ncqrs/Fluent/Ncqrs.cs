using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ncqrs.Config;

namespace Ncqrs.Fluent
{
    public static class Ncqrs
    {
		public static INcqrs Initialize()
		{
			return new NcqrsInitializer();
		}
	}

	public interface INcqrsHiddenMethods
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		bool Equals(object obj);
		[EditorBrowsable(EditorBrowsableState.Never)]
		int GetHashCode();
		[EditorBrowsable(EditorBrowsableState.Never)]
		string ToString();
	}

	public interface INcqrs : INcqrsHiddenMethods
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		void SetConfiguration(IEnvironmentConfiguration config);
	}

	public interface INcqrsConfig : INcqrsHiddenMethods
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		void AddRegistration<T>(Registration<T> registration);
	}

	public class Registration<T>
	{
		private readonly Func<IServiceLocator, T> setup;
		private readonly List<Type> interfaces = new List<Type>();
		private Action<T> activator;

		public Registration(Func<IServiceLocator, T> setup)
		{
			this.setup = setup;
		}

		public Registration<T> As<TInterface>()
		{
			this.interfaces.Add(typeof(TInterface));
			return this;
		}

		public Registration<T> Activation(Action<T> activator)
		{
			this.activator = activator;
			return this;
		}
	}

	public interface IServiceLocator
	{
		T GetInstance<T>();
		T GetInstance<T>(string name);
		T GetInstance<T>(Dictionary<string, object> parameters);
		T GetInstance<T>(string name, Dictionary<string, object> parameters);
	}
}