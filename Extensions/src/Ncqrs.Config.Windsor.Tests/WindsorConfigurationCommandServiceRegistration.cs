using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Moq;
using Ncqrs.Commanding;
using Ncqrs.Commanding.CommandExecution;
using Ncqrs.Commanding.ServiceModel;
using Xunit;

namespace Ncqrs.Config.Windsor.Tests
{
    public class when_using_windsor_to_registor_command_interceptors_and_handlers
    {
        WindsorContainer _container;
        FakeInterceptor _interceptor;
        Mock<ICommandExecutor<FakeCommand>> _handler;
        FakeCommand _testCommand;
        FakeInterceptor2 _interceptor2;

        public when_using_windsor_to_registor_command_interceptors_and_handlers()
        {
            _handler = new Mock<ICommandExecutor<FakeCommand>>();
            _handler.SetupAllProperties();
            _container = new WindsorContainer();
            _interceptor = new FakeInterceptor();
            _interceptor2 = new FakeInterceptor2();
            _container.Register(
                Component.For<IWindsorContainer>().Instance(_container),
                Component.For<ICommandExecutor<FakeCommand>>().Instance(_handler.Object),
                Component.For<ICommandServiceInterceptor>().Instance(_interceptor),
                Component.For<ICommandServiceInterceptor>().Instance(_interceptor2),
                Component.For<ICommandService>().ImplementedBy<WindsorCommandService>());
            var svc = _container.Resolve<ICommandService>();
            _testCommand = new FakeCommand();
            svc.Execute(_testCommand);
        }

        [Fact]
        public void it_should_call_the_handler()
        {
            _handler.Verify(h => h.Execute(_testCommand));
        }

        [Fact]
        public void it_should_call_both_interceptors()
        {
            Assert.True(_interceptor.OnBeforeExecutorResolvingCalled);
            Assert.True(_interceptor.OnBeforeExecutionCalled);
            Assert.True(_interceptor.OnAfterExecutionCalled);
            Assert.True(_interceptor2.OnBeforeExecutorResolvingCalled);
            Assert.True(_interceptor2.OnBeforeExecutionCalled);
            Assert.True(_interceptor2.OnAfterExecutionCalled);
        }

        [Fact]
        public void CanExecuteCommandRepeatedly()
        {
            var svc = _container.Resolve<ICommandService>();
            svc.Execute(new FakeCommand());
            svc.Execute(new FakeCommand());
            svc.Execute(new FakeCommand());
        }
    }

    public class FakeInterceptor2 : FakeInterceptor { }
    public class FakeInterceptor : ICommandServiceInterceptor
    {
        public bool OnBeforeExecutorResolvingCalled;
        public bool OnBeforeExecutionCalled;
        public bool OnAfterExecutionCalled;

        public void OnBeforeBeforeExecutorResolving(CommandContext context)
        {
            OnBeforeExecutorResolvingCalled = true;
            Assert.True(!context.ExecutorResolved);
            Assert.True(!context.ExecutorHasBeenCalled);
            Assert.Null(context.Exception);
        }
        public void OnBeforeExecution(CommandContext context)
        {
            OnBeforeExecutionCalled = true;
            Assert.True(context.ExecutorResolved);
            Assert.True(!context.ExecutorHasBeenCalled);
            Assert.Null(context.Exception);
        }
        public void OnAfterExecution(CommandContext context)
        {
            OnAfterExecutionCalled = true;
            Assert.True(context.ExecutorResolved);
            Assert.True(context.ExecutorHasBeenCalled);
            Assert.Null(context.Exception);
        }
    }

    public class FakeCommand : CommandBase { }
}