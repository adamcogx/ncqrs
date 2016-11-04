using System;
using Ncqrs.Eventing;
using FluentAssertions;
using Xunit;

namespace Ncqrs.EventBus.Tests
{
    public class EventDemultiplexerSpecification
    {
        [Fact]
        public void When_event_source_is_not_blocked_event_is_passed_through_system()
        {
            Guid eventSourceId = Guid.NewGuid();
            int enqueuedToProcessingCount = 0;

            var sut = new Demultiplexer();
            sut.EventDemultiplexed += (s, e) => { enqueuedToProcessingCount++; };
            sut.Demultiplex(CreateElement(eventSourceId));

            enqueuedToProcessingCount.Should().Be(1);

        }

        [Fact]
        public void Different_event_sources_does_not_block_each_other()
        {
            Guid firstEventSourceId = Guid.NewGuid();
            Guid secondEventSourceId = Guid.NewGuid();

            int enqueuedToProcessingCount = 0;

            var sut = new Demultiplexer();
            sut.EventDemultiplexed += (s, e) => { enqueuedToProcessingCount++; };
            sut.Demultiplex(CreateElement(firstEventSourceId));
            sut.Demultiplex(CreateElement(secondEventSourceId));

            enqueuedToProcessingCount.Should().Be(2);
        }

        [Fact]
        public void When_event_source_is_blocked_event_is_enqueued()
        {
            Guid eventSourceId = Guid.NewGuid();

            int enqueuedToProcessingCount = 0;

            var sut = new Demultiplexer();
            sut.EventDemultiplexed += (s, e) => { enqueuedToProcessingCount++; };
            sut.Demultiplex(CreateElement(eventSourceId));
            sut.Demultiplex(CreateElement(eventSourceId));

            enqueuedToProcessingCount.Should().Be(1);
        }

        private static IProcessingElement CreateElement(Guid sourceId)
        {
            return new FakeProcessingElement
                       {
                           GroupingKey = sourceId,
                           UniqueId = Guid.NewGuid().ToString()
                       };
        }
    }
}