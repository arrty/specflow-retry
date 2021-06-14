using SpecFlow.RetryCore;
using TechTalk.SpecFlow.Generator.Plugins;
using TechTalk.SpecFlow.Generator.UnitTestConverter;
using TechTalk.SpecFlow.Infrastructure;
using TechTalk.SpecFlow.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(GeneratorPlugin))]
namespace SpecFlow.RetryCore
{
    public class GeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(GeneratorPluginEvents generatorPluginEvents, GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            generatorPluginEvents.RegisterDependencies += (sender, args) =>
                {
                    args.ObjectContainer.RegisterTypeAs<RetryUnitTestFeatureGenerator, IFeatureGenerator>();
                    args.ObjectContainer.RegisterTypeAs<RetryUnitTestFeatureGeneratorProvider, IFeatureGeneratorProvider>("retry");
                    args.ObjectContainer.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestClassTagDecorator>("retry");
                    args.ObjectContainer.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestMethodTagDecorator>("retry");
                };
        }
    }
}

