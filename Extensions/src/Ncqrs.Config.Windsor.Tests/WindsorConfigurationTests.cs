using Castle.MicroKernel.Registration;
using Castle.Windsor;
using FluentAssertions;
using Xunit;

namespace Ncqrs.Config.Windsor.Tests
{
    public class WindsorConfigurationTests
    {
        [Fact]
        public void When_component_is_registered_it_should_be_retrievable()
        {
            var container = new WindsorContainer();
            container.Register(Component.For<IReplicant>().ImplementedBy<Nexus6>());

            var configuration = new WindsorConfiguration(container);

            IReplicant component;
            var success = configuration.TryGet(out component);

            success.Should().BeTrue();
            component.Should().NotBeNull();
            component.Should().BeOfType<Nexus6>();
        }

        [Fact]
        public void When_component_is_not_registered_it_should_not_be_retrievable()
        {
            var configuration = new WindsorConfiguration(new WindsorContainer());

            IReplicant component;
            var success = configuration.TryGet(out component);

            success.Should().BeFalse();
            component.Should().BeNull();
        }
    }

    public interface IReplicant { }
    public class Nexus6 : IReplicant {}
}
