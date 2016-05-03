using BoDi;
using SpecFlow.Retry;
using TechTalk.SpecFlow.Generator.Configuration;
using TechTalk.SpecFlow.Generator.Plugins;
using TechTalk.SpecFlow.Generator.UnitTestConverter;
using TechTalk.SpecFlow.Infrastructure;

[assembly:GeneratorPlugin(typeof(GeneratorPlugin))]
namespace SpecFlow.Retry
{
    public class GeneratorPlugin : IGeneratorPlugin
    {
        public void RegisterDependencies(ObjectContainer container)
        {
            container.RegisterTypeAs<RetryUnitTestFeatureGenerator, IFeatureGenerator>();
            container.RegisterTypeAs<RetryUnitTestFeatureGeneratorProvider, IFeatureGeneratorProvider>("retry");
        }

        public void RegisterCustomizations(ObjectContainer container, SpecFlowProjectConfiguration generatorConfiguration)
        {
            container.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestClassTagDecorator>("retry");
            container.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestMethodTagDecorator>("retry");
        }

        public void RegisterConfigurationDefaults(SpecFlowProjectConfiguration specFlowConfiguration)
        {            
        }
    }
}

