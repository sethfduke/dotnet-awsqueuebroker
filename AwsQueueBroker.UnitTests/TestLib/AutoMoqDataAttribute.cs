using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace AwsQueueBroker.UnitTests.TestLib
{
    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute() : base(Create) { }
        private static IFixture Create() => new Fixture().Customize(new AutoMoqCustomization());
    }
}