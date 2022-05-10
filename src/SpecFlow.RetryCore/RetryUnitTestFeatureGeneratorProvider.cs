namespace SpecFlow.RetryCore
{
    using TechTalk.SpecFlow.Generator.UnitTestConverter;
    using TechTalk.SpecFlow.Parser;

    public class RetryUnitTestFeatureGeneratorProvider : IFeatureGeneratorProvider
    {
        private readonly RetryUnitTestFeatureGenerator _unitTestFeatureGenerator;

        public RetryUnitTestFeatureGeneratorProvider(RetryUnitTestFeatureGenerator unitTestFeatureGenerator)
        {
            this._unitTestFeatureGenerator = unitTestFeatureGenerator;
        }

        public bool CanGenerate(SpecFlowDocument document)
        {
            return true;
        }

        public IFeatureGenerator CreateGenerator(SpecFlowDocument document)
        {
            return this._unitTestFeatureGenerator;
        }

        public int Priority => PriorityValues.Normal;
    }
}