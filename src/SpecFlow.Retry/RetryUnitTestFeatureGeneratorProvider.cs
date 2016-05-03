using TechTalk.SpecFlow.Generator.UnitTestConverter;
using TechTalk.SpecFlow.Parser;

namespace SpecFlow.Retry
{
    public class RetryUnitTestFeatureGeneratorProvider : IFeatureGeneratorProvider
    {
        private readonly RetryUnitTestFeatureGenerator _unitTestFeatureGenerator;

        public RetryUnitTestFeatureGeneratorProvider(RetryUnitTestFeatureGenerator unitTestFeatureGenerator)
        {
            _unitTestFeatureGenerator = unitTestFeatureGenerator;
        }

        public bool CanGenerate(SpecFlowFeature feature)
        {
            return true;
        }

        public IFeatureGenerator CreateGenerator(SpecFlowFeature feature)
        {
            return _unitTestFeatureGenerator;
        }

        public int Priority => PriorityValues.Normal;
    }
}