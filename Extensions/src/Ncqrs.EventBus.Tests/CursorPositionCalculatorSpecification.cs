using System;
using FluentAssertions;
using Xunit;

namespace Ncqrs.EventBus.Tests
{
    public class CursorPositionCalculatorSpecification
    {        
        [Fact]
        public void When_event_does_not_lengthen_the_sequence()
        {
            var sut = new CursorPositionCalculator(0);
            sut.Append(CreateProcessingEvent(2));

            sut.Count.Should().Be(1);
            sut.SequenceLength.Should().Be(0);
        }        

        [Fact]
        public void When_event_lengthens_the_sequence()
        {
            var sut = new CursorPositionCalculator(0);
            sut.Append(CreateProcessingEvent(1));

            sut.Count.Should().Be(1);
            sut.SequenceLength.Should().Be(1);   
        }
        
        [Fact]
        public void When_event_fills_gap_in_sequence_sequence_length_is_incremented_by_gap_size()
        {
            var sut = new CursorPositionCalculator(0);
            sut.Append(CreateProcessingEvent(2));
            sut.Append(CreateProcessingEvent(3));
            sut.Append(CreateProcessingEvent(5));

            sut.Append(CreateProcessingEvent(1));

            sut.Count.Should().Be(4);
            sut.SequenceLength.Should().Be(3);            
        }

        [Fact]
        public void When_clearing_sequence()
        {
            var sut = new CursorPositionCalculator(0);
            sut.Append(CreateProcessingEvent(1));
            sut.Append(CreateProcessingEvent(2));
            sut.Append(CreateProcessingEvent(4));

            sut.ClearSequence();

            sut.Count.Should().Be(1);
            sut.SequenceLength.Should().Be(0);
        }        

        private static IProcessingElement CreateProcessingEvent(int sequence)
        {
            return new FakeProcessingElement {SequenceNumber = sequence};
        }
    }
}