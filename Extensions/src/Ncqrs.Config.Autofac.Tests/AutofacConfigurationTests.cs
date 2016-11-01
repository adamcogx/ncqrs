using Autofac;
using FluentAssertions;
using Xunit;

namespace Ncqrs.Config.Autofac.Tests
{
    public class AutofacConfigurationTests
    {
        [Fact]
        public void When_component_is_registered_it_should_be_retrievable()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<Nexus6>().As<IReplicant>();

            var container = builder.Build();
            var configuration = new AutofacConfiguration(container);

            IReplicant component;
            var success = configuration.TryGet(out component);

            success.Should().BeTrue();
            component.Should().NotBeNull();
            component.Should().BeAssignableTo<IReplicant>();
        }

        [Fact]
        public void When_component_is_not_registered_it_should_not_be_retrievable()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var configuration = new AutofacConfiguration(container);

            IReplicant component;
            var success = configuration.TryGet(out component);

            success.Should().BeFalse();
            component.Should().BeNull();
        }
    }

    public interface IReplicant { }

    public class Nexus6 : IReplicant { }
}